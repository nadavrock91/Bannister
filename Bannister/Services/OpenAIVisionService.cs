using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Bannister.Services;

public class OpenAIVisionService
{
    private const string ChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const string VisionModel = "gpt-4o-mini";
    private const int MaxImageLongEdge = 1024;
    private const int JpegQuality = 80;
    private const int BaselineDelayMs = 250;
    private const int InitialBackoffMs = 2000;
    private const int MaxBackoffMs = 30000;
    private const int MaxRateLimitRetries = 5;
    private const string NamingPrompt =
        "You are naming an image file. Look at this image and produce a short descriptive lowercase snake_case filename (no extension). Use only lowercase letters, digits, and underscores. Keep it to 3-7 words maximum. Be specific about the visible subject. Examples: red_sneakers_on_concrete, woman_drinking_coffee_window, abandoned_warehouse_interior_dusk. Output ONLY the filename, no explanation, no quotes, no extension.";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly OpenAIKeyService _keyService;
    private readonly HttpClient _apiClient;
    private DateTime _lastCallStartedAt = DateTime.MinValue;
    private readonly object _pacingLock = new();

    public OpenAIVisionService(OpenAIKeyService keyService)
    {
        _keyService = keyService;
        _apiClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<VisionNameResult> SuggestImageNameAsync(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return VisionNameResult.Fail("Image file was not found.");

        if (!SupportedExtensions.Contains(Path.GetExtension(imagePath)))
            return VisionNameResult.Fail("Only JPG, PNG, and WEBP images are supported.");

        var apiKey = await _keyService.GetDecryptedKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
            return VisionNameResult.Fail("Configure API key first.");

        try
        {
            var imageBytes = EncodeDownscaledJpeg(imagePath);
            if (imageBytes == null || imageBytes.Length == 0)
                return VisionNameResult.Fail("Could not read image.");

            var base64 = Convert.ToBase64String(imageBytes);

            var requestBody = new
            {
                model = VisionModel,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = NamingPrompt
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/jpeg;base64,{base64}"
                                }
                            }
                        }
                    }
                },
                max_tokens = 60
            };

            string responseJson;
            var backoffMs = InitialBackoffMs;
            var attempt = 0;

            while (true)
            {
                attempt++;
                await EnforceBaselinePacingAsync();

                using var request = BuildRequest(apiKey, requestBody);
                using var response = await _apiClient.SendAsync(request);

                if ((int)response.StatusCode == 429)
                {
                    responseJson = await response.Content.ReadAsStringAsync();

                    if (attempt >= MaxRateLimitRetries)
                    {
                        return VisionNameResult.Fail(
                            $"Rate limit persists after {MaxRateLimitRetries} retries. Check your OpenAI plan tier or lower request volume.",
                            TruncateRawResponse(responseJson));
                    }

                    var waitMs = GetRateLimitWaitMs(response, backoffMs);
                    try
                    {
                        await Task.Delay(waitMs);
                    }
                    catch
                    {
                    }

                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
                    lock (_pacingLock)
                    {
                        _lastCallStartedAt = DateTime.UtcNow;
                    }

                    continue;
                }

                responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return VisionNameResult.Fail(MapError(response.StatusCode, responseJson), TruncateRawResponse(responseJson));

                break;
            }

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);
            var rawName = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            var raw = rawName ?? "";
            var sanitized = SanitizeFilename(raw);

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                string reason;
                if (string.IsNullOrWhiteSpace(raw))
                    reason = "Empty response from API";
                else if (LooksLikeRefusal(raw))
                    reason = "Model declined to name image";
                else
                    reason = "Response sanitized to empty";

                return VisionNameResult.Fail(reason, raw);
            }

            return VisionNameResult.Ok(sanitized, raw);
        }
        catch (TaskCanceledException ex)
        {
            return VisionNameResult.Fail($"Network timeout: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return VisionNameResult.Fail($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return VisionNameResult.Fail($"Vision naming failed: {ex.Message}");
        }
    }

    private async Task EnforceBaselinePacingAsync(CancellationToken ct = default)
    {
        TimeSpan waitFor;
        lock (_pacingLock)
        {
            var elapsed = DateTime.UtcNow - _lastCallStartedAt;
            var remaining = TimeSpan.FromMilliseconds(BaselineDelayMs) - elapsed;
            waitFor = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            _lastCallStartedAt = DateTime.UtcNow + waitFor;
        }

        if (waitFor > TimeSpan.Zero)
            await Task.Delay(waitFor, ct);
    }

    private static HttpRequestMessage BuildRequest(string apiKey, object requestBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");
        return request;
    }

    private static int GetRateLimitWaitMs(HttpResponseMessage response, int backoffMs)
    {
        var waitMs = backoffMs;
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
        {
            waitMs = Math.Max((int)delta.TotalMilliseconds, backoffMs);
        }
        else if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
        {
            var untilDate = (int)(date - DateTimeOffset.UtcNow).TotalMilliseconds;
            waitMs = Math.Max(untilDate, backoffMs);
        }

        return Math.Min(Math.Max(waitMs, 0), MaxBackoffMs);
    }

    private static byte[]? EncodeDownscaledJpeg(string imagePath)
    {
        using var source = SKBitmap.Decode(imagePath);
        if (source == null)
            return null;

        SKBitmap? resized = null;
        var bitmapToEncode = source;
        var longEdge = Math.Max(source.Width, source.Height);

        if (longEdge > MaxImageLongEdge)
        {
            var scale = MaxImageLongEdge / (double)longEdge;
            var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            resized = source.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
            if (resized == null)
                return null;

            bitmapToEncode = resized;
        }

        try
        {
            using var image = SKImage.FromBitmap(bitmapToEncode);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            return data?.ToArray();
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static string SanitizeFilename(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var s = raw.Trim().ToLowerInvariant();
        s = s.Trim('"', '\'', '`');
        s = Regex.Replace(s, @"\s+", "_");
        s = Regex.Replace(s, @"[^a-z0-9_\-]", "");
        s = Regex.Replace(s, @"_+", "_");
        s = s.Trim('_', '-');
        if (s.Length > 80)
            s = s.Substring(0, 80).TrimEnd('_', '-');

        return s;
    }

    private static bool LooksLikeRefusal(string raw)
    {
        var lower = raw.ToLowerInvariant();
        return lower.Contains("i cannot") ||
               lower.Contains("i can't") ||
               lower.Contains("i am unable") ||
               lower.Contains("i'm unable") ||
               lower.Contains("i'm sorry") ||
               lower.Contains("i am sorry") ||
               lower.Contains("not appropriate") ||
               lower.Contains("cannot assist");
    }

    private static string TruncateRawResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        return raw.Length > 1000 ? raw.Substring(0, 1000) + "..." : raw;
    }

    private static string MapError(HttpStatusCode statusCode, string responseJson)
    {
        var apiMessage = TryGetApiErrorMessage(responseJson);

        if (statusCode == HttpStatusCode.Unauthorized)
            return "API key was rejected. Re-configure your key.";

        if ((int)statusCode == 429)
            return "Rate limit or quota exceeded. Check your OpenAI billing.";

        return !string.IsNullOrWhiteSpace(apiMessage)
            ? apiMessage
            : $"OpenAI Vision request failed with status {(int)statusCode} ({statusCode}).";
    }

    private static string? TryGetApiErrorMessage(string responseJson)
    {
        try
        {
            var error = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseJson);
            return error?.Error?.Message;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class OpenAIErrorResponse
    {
        [JsonPropertyName("error")]
        public OpenAIError? Error { get; set; }
    }

    private sealed class OpenAIError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}

public sealed class VisionNameResult
{
    public bool Success { get; init; }
    public string? SuggestedName { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RawResponse { get; init; }

    public static VisionNameResult Ok(string suggestedName, string? rawResponse = null) =>
        new() { Success = true, SuggestedName = suggestedName, RawResponse = rawResponse };

    public static VisionNameResult Fail(string errorMessage, string? rawResponse = null) =>
        new() { Success = false, ErrorMessage = errorMessage, RawResponse = rawResponse };
}
