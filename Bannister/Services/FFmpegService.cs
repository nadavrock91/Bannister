using System.Diagnostics;

namespace Bannister.Services;

public class FFmpegService
{
    private const string FFmpegFileName = "ffmpeg.exe";

    public async Task<string> EnsureFFmpegAsync()
    {
#if WINDOWS
        string targetPath = Path.Combine(FileSystem.AppDataDirectory, FFmpegFileName);
        if (File.Exists(targetPath))
            return targetPath;

        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        await using var source = await FileSystem.OpenAppPackageFileAsync(FFmpegFileName);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target);
        return targetPath;
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Bundled FFmpeg is only available on Windows.");
#endif
    }

    public async Task<(bool Success, string ErrorMessage)> RunCutAsync(
        string ffmpegPath,
        string inputPath,
        string startTime,
        string endTime,
        string outputPath)
    {
#if WINDOWS
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("-ss");
        startInfo.ArgumentList.Add(startTime);
        startInfo.ArgumentList.Add("-to");
        startInfo.ArgumentList.Add(endTime);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("copy");
        startInfo.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            return (true, "");

        string message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        return (false, message.Trim());
#else
        await Task.CompletedTask;
        return (false, "Bundled FFmpeg cutting is only available on Windows.");
#endif
    }
}
