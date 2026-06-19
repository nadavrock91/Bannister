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

    public OpenAIImageService(OpenAIKeyService keyService)
    {
        _keyService = keyService;
        _apiClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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
                model = "gpt-image-1-mini",
                prompt = prompt.Trim(),
                n = 1,
                size = "1024x1024",
                quality = "low"
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
            var b64Json = parsed?.Data?.FirstOrDefault()?.B64Json;
            if (string.IsNullOrWhiteSpace(b64Json))
                return GenerateImageResult.Fail("Image generation succeeded, but no image data was returned.");

            var imageBytes = Convert.FromBase64String(b64Json);
            return GenerateImageResult.Ok(null, imageBytes);
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
        var combinedMessage = $"{apiMessage}\n{responseJson}";

        if (combinedMessage.Contains("must be verified to use this model", StringComparison.OrdinalIgnoreCase) ||
            combinedMessage.Contains("organization must be verified", StringComparison.OrdinalIgnoreCase))
        {
            return "Your OpenAI organization must be verified to use GPT Image models. Visit platform.openai.com/settings to complete verification.";
        }

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
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }
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
    public static GenerateImageResult Ok(string? imageUrl, byte[] imageBytes) =>
        new(true, imageUrl, imageBytes, null);

    public static GenerateImageResult Fail(string errorMessage) =>
        new(false, null, null, errorMessage);
}
