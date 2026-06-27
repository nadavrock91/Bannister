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

            using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

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

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _apiClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return VisionNameResult.Fail(MapError(response.StatusCode, responseJson));

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);
            var rawName = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            var sanitized = SanitizeFilename(rawName ?? "");

            return string.IsNullOrWhiteSpace(sanitized)
                ? VisionNameResult.Fail("Vision response did not contain a usable filename.")
                : VisionNameResult.Ok(sanitized);
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

public sealed record VisionNameResult(bool Success, string? SuggestedName, string? ErrorMessage)
{
    public static VisionNameResult Ok(string name) => new(true, name, null);

    public static VisionNameResult Fail(string error) => new(false, null, error);
}
