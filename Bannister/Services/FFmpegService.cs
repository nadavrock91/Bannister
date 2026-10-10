using System.Diagnostics;
using System.IO.Compression;

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
        return (false, "FFmpeg cutting is only available on Windows.");
#endif
    }
}
