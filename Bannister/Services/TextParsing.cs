namespace Bannister.Services;

public static class TextParsing
{
    public static string NormalizeLineEndings(string? text) =>
        (text ?? string.Empty).ReplaceLineEndings("\n");
}
