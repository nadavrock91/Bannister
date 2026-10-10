using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Bannister.Services;

public class FFmpegService
{
    private const string FFmpegFileName = "ffmpeg.exe";
    private const string DownloadUrl =
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
    private const string ZipEntryName =
        "ffmpeg-master-latest-win64-gpl/bin/ffmpeg.exe";

    public async Task<string> EnsureFFmpegAsync(
        IProgress<(long bytesReceived, long totalBytes)>? progress = null)
    {
#if WINDOWS
        string targetPath = Path.Combine(FileSystem.AppDataDirectory, FFmpegFileName);
        if (File.Exists(targetPath))
            return targetPath;

        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        Directory.CreateDirectory(FileSystem.CacheDirectory);

        string zipPath = Path.Combine(FileSystem.CacheDirectory, "ffmpeg-btbn.zip");
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        using (var http = new HttpClient())
        using (var response = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var zipStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(zipPath);
            var buffer = new byte[81920];
            long bytesReceived = 0;
            int bytesRead;
            while ((bytesRead = await zipStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                bytesReceived += bytesRead;
                progress?.Report((bytesReceived, totalBytes));
            }
        }

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            var entry = archive.GetEntry(ZipEntryName)
                ?? throw new FileNotFoundException("ffmpeg.exe was not found in the downloaded FFmpeg archive.");

            string tempExtractPath = Path.Combine(FileSystem.CacheDirectory, FFmpegFileName);
            if (File.Exists(tempExtractPath))
                File.Delete(tempExtractPath);

            entry.ExtractToFile(tempExtractPath, true);
            File.Copy(tempExtractPath, targetPath, true);
            File.Delete(tempExtractPath);
        }

        File.Delete(zipPath);
        return targetPath;
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("FFmpeg cutting is only available on Windows.");
#endif
    }

    public async Task<(bool success, string error)> RunCutAsync(
        string ffmpegPath,
        string inputPath,
        string startTime,
        string endTime,
        string outputPath,
        IProgress<double>? progress = null,
        TimeSpan clipStart = default,
        TimeSpan clipEnd = default)
    {
#if WINDOWS
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
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
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = false;
        process.EnableRaisingEvents = true;

        var errorLines = new StringBuilder();
        if (clipStart == default && TryParseTime(startTime, out var parsedStart))
            clipStart = parsedStart;
        if (clipEnd == default && TryParseTime(endTime, out var parsedEnd))
            clipEnd = parsedEnd;
        TimeSpan totalDuration = clipEnd > clipStart
            ? clipEnd - clipStart
            : TimeSpan.Zero;

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data == null)
                return;

            errorLines.AppendLine(e.Data);
            if (progress != null && e.Data.Contains("time="))
            {
                var timeMatch = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.\d+)");
                if (timeMatch.Success)
                {
                    int h = int.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    int m = int.Parse(timeMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                    double sec = double.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                    var current = new TimeSpan(h, m, 0) + TimeSpan.FromSeconds(sec);
                    double pct = totalDuration.TotalSeconds > 0
                        ? Math.Min(1.0, current.TotalSeconds / totalDuration.TotalSeconds)
                        : 0;
                    progress.Report(pct);
                }
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            return (true, "");

        return (false, errorLines.ToString().Trim());
#else
        await Task.CompletedTask;
        return (false, "FFmpeg cutting is only available on Windows.");
#endif
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Trim().Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
        {
            time = new TimeSpan(0, 0, minutes, seconds);
            return true;
        }

        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time);
    }
}
