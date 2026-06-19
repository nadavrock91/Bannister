using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bannister.Services;

public class OpenAIImageService
{
    private const string ImageGenerationUrl = "https://api.openai.com/v1/images/generations";
    private readonly OpenAIKeyService _keyService;
    private readonly HttpClient _apiClient;
    private readonly HttpClient _downloadClient;

    public OpenAIImageService(OpenAIKeyService keyService)
    {
        _keyService = keyService;
        _apiClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _downloadClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<GenerateImageResult> GenerateImageAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return GenerateImageResult.Fail("Enter a prompt first.");

        var apiKey = await _keyService.GetDecryptedKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
            return GenerateImageResult.Fail("Configure API key first.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ImageGenerationUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model = "dall-e-2",
                prompt = prompt.Trim(),
                n = 1,
                size = "1024x1024",
                response_format = "url"
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            using var response = await _apiClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return GenerateImageResult.Fail(MapError(response.StatusCode, responseJson));

            var parsed = JsonSerializer.Deserialize<ImageGenerationResponse>(responseJson);
            var imageUrl = parsed?.Data?.FirstOrDefault()?.Url;
            if (string.IsNullOrWhiteSpace(imageUrl))
                return GenerateImageResult.Fail("Image generation succeeded, but no image URL was returned.");

            var imageBytes = await _downloadClient.GetByteArrayAsync(imageUrl);
            return GenerateImageResult.Ok(imageUrl, imageBytes);
        }
        catch (TaskCanceledException ex)
        {
            return GenerateImageResult.Fail($"Network timeout: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return GenerateImageResult.Fail($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return GenerateImageResult.Fail($"Image generation failed: {ex.Message}");
        }
    }

    private static string MapError(HttpStatusCode statusCode, string responseJson)
    {
        var apiMessage = TryGetApiErrorMessage(responseJson);
        if (statusCode == HttpStatusCode.Unauthorized)
            return "API key was rejected. Re-configure your key.";

        if ((int)statusCode == 429)
            return "Rate limit or quota exceeded. Check your OpenAI billing.";

        if (statusCode == HttpStatusCode.BadRequest &&
            responseJson.Contains("content_policy_violation", StringComparison.OrdinalIgnoreCase))
        {
            return "Prompt was rejected by content policy. Try a different prompt.";
        }

        return !string.IsNullOrWhiteSpace(apiMessage)
            ? apiMessage
            : $"OpenAI request failed with status {(int)statusCode} ({statusCode}).";
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

    private sealed class ImageGenerationResponse
    {
        [JsonPropertyName("data")]
        public List<ImageData>? Data { get; set; }
    }

    private sealed class ImageData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
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

public sealed record GenerateImageResult(
    bool Success,
    string? ImageUrl,
    byte[]? ImageBytes,
    string? ErrorMessage)
{
    public static GenerateImageResult Ok(string imageUrl, byte[] imageBytes) =>
        new(true, imageUrl, imageBytes, null);

    public static GenerateImageResult Fail(string errorMessage) =>
        new(false, null, null, errorMessage);
}
