using System.Text.RegularExpressions;

namespace Bannister.Services;

/// <summary>
/// Represents a single surgical edit command.
/// </summary>
public class SurgicalEditCommand
{
    public string Type { get; set; } = ""; // "delete", "update", "insert", "reorder"
    public int LineNumber { get; set; }
    public int? TargetPosition { get; set; } // For reorder
    public Dictionary<string, string> Fields { get; set; } = new();
    
    public string GetSummary()
    {
        return Type switch
        {
            "delete" => $"DELETE line {LineNumber}",
            "update" => $"UPDATE line {LineNumber}: {string.Join(", ", Fields.Keys)}",
            "insert" => $"INSERT after line {LineNumber}: {(Fields.TryGetValue("Script", out var s) && !string.IsNullOrEmpty(s) ? Truncate(s, 50) : Truncate(Fields.GetValueOrDefault("Visual", ""), 50))}",
            "reorder" => $"MOVE line {LineNumber} to position {TargetPosition}",
            _ => $"UNKNOWN: {Type}"
        };
    }
    
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 3)] + "...";
}

/// <summary>
/// Result of parsing surgical edit commands.
/// </summary>
public class SurgicalEditParseResult
{
    public bool Success { get; set; }
    public string? DraftName { get; set; }
    public int? ExpectedLineCount { get; set; }
    public List<SurgicalEditCommand> Commands { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string? RawInput { get; set; }
}

/// <summary>
/// Parses and validates surgical edit commands from LLM output.
/// </summary>
public static class SurgicalEditParser
{
    /// <summary>
    /// Parse surgical edit commands from LLM output.
    /// </summary>
    public static SurgicalEditParseResult Parse(string input)
    {
        var result = new SurgicalEditParseResult { RawInput = input };
        
        // Extract code block if present
        var codeMatch = Regex.Match(input, @"```csharp\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        string code = codeMatch.Success ? codeMatch.Groups[1].Value : input;
        
        // Parse header: Draft name and line count
        var draftMatch = Regex.Match(code, @"//\s*Draft:\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (draftMatch.Success)
        {
            result.DraftName = draftMatch.Groups[1].Value;
        }
        
        var linesMatch = Regex.Match(code, @"//\s*Lines:\s*(\d+)", RegexOptions.IgnoreCase);
        if (linesMatch.Success)
        {
            result.ExpectedLineCount = int.Parse(linesMatch.Groups[1].Value);
        }
        
        // Parse DELETE commands: delete[5];
        foreach (Match match in Regex.Matches(code, @"delete\[(\d+)\]\s*;"))
        {
            result.Commands.Add(new SurgicalEditCommand
            {
                Type = "delete",
                LineNumber = int.Parse(match.Groups[1].Value)
            });
        }
        
        // Parse REORDER commands: reorder[10] = 3;
        foreach (Match match in Regex.Matches(code, @"reorder\[(\d+)\]\s*=\s*(\d+)\s*;"))
        {
            result.Commands.Add(new SurgicalEditCommand
            {
                Type = "reorder",
                LineNumber = int.Parse(match.Groups[1].Value),
                TargetPosition = int.Parse(match.Groups[2].Value)
            });
        }
        
        // Parse UPDATE commands: update[3].Field = "value";
        var updateCommands = new Dictionary<int, SurgicalEditCommand>();
        foreach (Match match in Regex.Matches(code, @"update\[(\d+)\]\.(\w+)\s*=\s*""((?:[^""\\]|\\.)*)"""))
        {
            int lineNum = int.Parse(match.Groups[1].Value);
            string field = match.Groups[2].Value;
            string value = UnescapeString(match.Groups[3].Value);
            
            if (!updateCommands.TryGetValue(lineNum, out var cmd))
            {
                cmd = new SurgicalEditCommand { Type = "update", LineNumber = lineNum };
                updateCommands[lineNum] = cmd;
            }
            cmd.Fields[field] = value;
        }
        result.Commands.AddRange(updateCommands.Values);
        
        // Parse INSERT commands: insert[7].Field = "value";
        var insertCommands = new Dictionary<int, SurgicalEditCommand>();
        foreach (Match match in Regex.Matches(code, @"insert\[(\d+)\]\.(\w+)\s*=\s*""((?:[^""\\]|\\.)*)"""))
        {
            int lineNum = int.Parse(match.Groups[1].Value);
            string field = match.Groups[2].Value;
            string value = UnescapeString(match.Groups[3].Value);
            
            if (!insertCommands.TryGetValue(lineNum, out var cmd))
            {
                cmd = new SurgicalEditCommand { Type = "insert", LineNumber = lineNum };
                insertCommands[lineNum] = cmd;
            }
            cmd.Fields[field] = value;
        }
        result.Commands.AddRange(insertCommands.Values);
        
        // Sort commands for predictable order
        result.Commands = result.Commands
            .OrderBy(c => c.Type switch { "delete" => 0, "update" => 1, "insert" => 2, "reorder" => 3, _ => 4 })
            .ThenBy(c => c.LineNumber)
            .ToList();
        
        result.Success = result.Commands.Count > 0;
        
        if (!result.Success && result.Commands.Count == 0)
        {
            result.Errors.Add("No valid commands found in input");
        }
        
        return result;
    }
    
    /// <summary>
    /// Validate commands against current draft state.
    /// </summary>
    public static List<string> ValidateCommands(
        SurgicalEditParseResult parseResult,
        string currentDraftName,
        int currentLineCount)
    {
        var errors = new List<string>();
        
        // Check draft name matches
        if (!string.IsNullOrEmpty(parseResult.DraftName) && 
            !parseResult.DraftName.Equals(currentDraftName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Draft name mismatch: commands are for \"{parseResult.DraftName}\" but current draft is \"{currentDraftName}\"");
        }
        
        // Check line count matches
        if (parseResult.ExpectedLineCount.HasValue && parseResult.ExpectedLineCount.Value != currentLineCount)
        {
            errors.Add($"Line count mismatch: commands expect {parseResult.ExpectedLineCount} lines but draft has {currentLineCount} lines");
        }
        
        // Validate individual commands
        foreach (var cmd in parseResult.Commands)
        {
            if (cmd.Type == "delete" || cmd.Type == "update")
            {
                if (cmd.LineNumber < 1 || cmd.LineNumber > currentLineCount)
                {
                    errors.Add($"{cmd.Type.ToUpper()} line {cmd.LineNumber}: line doesn't exist (draft has {currentLineCount} lines)");
                }
            }
            else if (cmd.Type == "insert")
            {
                if (cmd.LineNumber < 0 || cmd.LineNumber > currentLineCount)
                {
                    errors.Add($"INSERT after line {cmd.LineNumber}: invalid position (use 0-{currentLineCount})");
                }
            }
            else if (cmd.Type == "reorder")
            {
                if (cmd.LineNumber < 1 || cmd.LineNumber > currentLineCount)
                {
                    errors.Add($"REORDER line {cmd.LineNumber}: line doesn't exist");
                }
                if (cmd.TargetPosition < 1 || cmd.TargetPosition > currentLineCount)
                {
                    errors.Add($"REORDER to position {cmd.TargetPosition}: invalid position");
                }
            }
        }
        
        return errors;
    }
    
    private static string UnescapeString(string s)
    {
        return s.Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\\\", "\\");
    }
}
