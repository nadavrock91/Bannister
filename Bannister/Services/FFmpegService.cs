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

    public async Task<(bool success, string error)> TruncateSilenceAsync(
        string ffmpegPath,
        string inputPath,
        string outputPath,
        double thresholdDb,
        double minDurationSec,
        double truncateToSec,
        IProgress<double>? progress = null)
    {
#if WINDOWS
        var analysis = await AnalyseSilenceAsync(ffmpegPath, inputPath, thresholdDb, minDurationSec);
        if (!analysis.success)
            return (false, analysis.error);

        double silenceSeconds = analysis.silences.Sum(s => Math.Max(0, s.end - s.start));
        var inputDuration = analysis.estimatedDuration + TimeSpan.FromSeconds(silenceSeconds);
        var keepSegments = BuildKeepSegments(analysis.silences, inputDuration.TotalSeconds, truncateToSec);
        if (keepSegments.Count == 0)
            return (false, "No speech segments were found after silence analysis.");

        Directory.CreateDirectory(FileSystem.CacheDirectory);
        string segmentsFile = Path.Combine(FileSystem.CacheDirectory, "segments.txt");
        string escapedInputPath = inputPath.Replace("\\", "/", StringComparison.Ordinal).Replace("'", "'\\''", StringComparison.Ordinal);
        var segmentsBuilder = new StringBuilder();
        segmentsBuilder.AppendLine("ffconcat version 1.0");
        foreach (var segment in keepSegments)
        {
            segmentsBuilder.AppendLine(CultureInfo.InvariantCulture, $"file '{escapedInputPath}'");
            segmentsBuilder.AppendLine(CultureInfo.InvariantCulture, $"inpoint {segment.start:0.###}");
            segmentsBuilder.AppendLine(CultureInfo.InvariantCulture, $"outpoint {segment.end:0.###}");
        }
        await File.WriteAllTextAsync(segmentsFile, segmentsBuilder.ToString());

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("concat");
        startInfo.ArgumentList.Add("-safe");
        startInfo.ArgumentList.Add("0");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(segmentsFile);
        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("aac");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("44100");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add("fps=30");
        startInfo.ArgumentList.Add("-af");
        startInfo.ArgumentList.Add("aresample=async=1");
        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = startInfo };
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = false;
        process.EnableRaisingEvents = true;

        var errorLines = new StringBuilder();
        TimeSpan totalDuration = TimeSpan.FromSeconds(keepSegments.Sum(s => Math.Max(0, s.end - s.start)));

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data == null)
                return;

            errorLines.AppendLine(e.Data);

            var durationMatch = Regex.Match(e.Data, @"Duration:\s*(\d+):(\d+):(\d+\.\d+)");
            if (durationMatch.Success)
            {
                int h = int.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                int m = int.Parse(durationMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                double sec = double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                totalDuration = new TimeSpan(h, m, 0) + TimeSpan.FromSeconds(sec);
            }

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

        try
        {
            process.Start();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
        }
        finally
        {
            if (File.Exists(segmentsFile))
                File.Delete(segmentsFile);
        }

        if (process.ExitCode == 0)
            return (true, "");

        return (false, errorLines.ToString().Trim());
#else
        await Task.CompletedTask;
        return (false, "FFmpeg silence truncation is only available on Windows.");
#endif
    }

    public async Task<(bool success, string error)> CutSegmentsAsync(
        string ffmpegPath,
        string inputPath,
        string outputPath,
        List<(double start, double end)> keepSegments,
        IProgress<double>? progress = null)
    {
#if WINDOWS
        keepSegments = keepSegments
            .Where(s => s.end > s.start)
            .OrderBy(s => s.start)
            .ToList();

        if (keepSegments.Count == 0)
            return (false, "No enabled transcript segments were available to compile.");

        Directory.CreateDirectory(FileSystem.CacheDirectory);
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string tempDir = Path.Combine(FileSystem.CacheDirectory, $"cut_work_{timestamp}");
        Directory.CreateDirectory(tempDir);
        string concatFile = Path.Combine(tempDir, "segments.txt");
        var tempSegmentFiles = new List<string>();
        var errorLines = new StringBuilder();

        try
        {
            for (int i = 0; i < keepSegments.Count; i++)
            {
                var segment = keepSegments[i];
                string segmentPath = Path.Combine(tempDir, $"seg_{i}.mp4");
                tempSegmentFiles.Add(segmentPath);

                var segmentStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true
                };
                segmentStartInfo.ArgumentList.Add("-y");
                segmentStartInfo.ArgumentList.Add("-ss");
                segmentStartInfo.ArgumentList.Add(segment.start.ToString("0.###", CultureInfo.InvariantCulture));
                segmentStartInfo.ArgumentList.Add("-to");
                segmentStartInfo.ArgumentList.Add(segment.end.ToString("0.###", CultureInfo.InvariantCulture));
                segmentStartInfo.ArgumentList.Add("-i");
                segmentStartInfo.ArgumentList.Add(inputPath);
                segmentStartInfo.ArgumentList.Add("-c:v");
                segmentStartInfo.ArgumentList.Add("libx264");
                segmentStartInfo.ArgumentList.Add("-crf");
                segmentStartInfo.ArgumentList.Add("18");
                segmentStartInfo.ArgumentList.Add("-preset");
                segmentStartInfo.ArgumentList.Add("fast");
                segmentStartInfo.ArgumentList.Add("-c:a");
                segmentStartInfo.ArgumentList.Add("aac");
                segmentStartInfo.ArgumentList.Add("-ar");
                segmentStartInfo.ArgumentList.Add("44100");
                segmentStartInfo.ArgumentList.Add("-ac");
                segmentStartInfo.ArgumentList.Add("2");
                segmentStartInfo.ArgumentList.Add("-af");
                segmentStartInfo.ArgumentList.Add("aresample=async=1");
                segmentStartInfo.ArgumentList.Add(segmentPath);

                using var segmentProcess = new Process { StartInfo = segmentStartInfo };
                segmentProcess.EnableRaisingEvents = true;
                double segmentDuration = Math.Max(0.001, Math.Min(segment.end, 7200) - segment.start);
                int segmentIndex = i;
                segmentProcess.ErrorDataReceived += (s, e) =>
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
                            double segmentProgress = Math.Min(1.0, current.TotalSeconds / segmentDuration);
                            progress.Report(0.95 * (segmentIndex + segmentProgress) / keepSegments.Count);
                        }
                    }
                };

                segmentProcess.Start();
                segmentProcess.BeginErrorReadLine();
                await segmentProcess.WaitForExitAsync();

                if (segmentProcess.ExitCode != 0)
                    return (false, errorLines.ToString().Trim());

                progress?.Report(0.95 * (i + 1) / keepSegments.Count);
            }

            var concatBuilder = new StringBuilder();
            concatBuilder.AppendLine("ffconcat version 1.0");
            foreach (string segmentPath in tempSegmentFiles)
            {
                string escapedSegmentPath = segmentPath.Replace("\\", "/", StringComparison.Ordinal).Replace("'", "'\\''", StringComparison.Ordinal);
                concatBuilder.AppendLine($"file '{escapedSegmentPath}'");
            }
            await File.WriteAllTextAsync(concatFile, concatBuilder.ToString());

            var concatStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            };
            concatStartInfo.ArgumentList.Add("-y");
            concatStartInfo.ArgumentList.Add("-f");
            concatStartInfo.ArgumentList.Add("concat");
            concatStartInfo.ArgumentList.Add("-safe");
            concatStartInfo.ArgumentList.Add("0");
            concatStartInfo.ArgumentList.Add("-i");
            concatStartInfo.ArgumentList.Add(concatFile);
            concatStartInfo.ArgumentList.Add("-c");
            concatStartInfo.ArgumentList.Add("copy");
            concatStartInfo.ArgumentList.Add(outputPath);

            using var concatProcess = new Process { StartInfo = concatStartInfo };
            concatProcess.EnableRaisingEvents = true;
            concatProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    errorLines.AppendLine(e.Data);
            };

            concatProcess.Start();
            concatProcess.BeginErrorReadLine();
            await concatProcess.WaitForExitAsync();

            if (concatProcess.ExitCode != 0)
                return (false, errorLines.ToString().Trim());

            progress?.Report(1.0);
            return (true, "");
        }
        finally
        {
            foreach (string segmentPath in tempSegmentFiles)
            {
                if (File.Exists(segmentPath))
                    File.Delete(segmentPath);
            }
            if (File.Exists(concatFile))
                File.Delete(concatFile);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir);
        }
#else
        await Task.CompletedTask;
        return (false, "FFmpeg segment compilation is only available on Windows.");
#endif
    }

    public async Task<TimeSpan> GetVideoDurationAsync(string ffmpegPath, string inputPath)
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
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Match durationMatch = Regex.Match(
            standardError,
            @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
        if (!durationMatch.Success)
            return TimeSpan.Zero;

        int hours = int.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        int minutes = int.Parse(durationMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        double seconds = double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        return new TimeSpan(hours, minutes, 0) + TimeSpan.FromSeconds(seconds);
#else
        await Task.CompletedTask;
        return TimeSpan.Zero;
#endif
    }

    public async Task<(bool success, string error, TimeSpan estimatedDuration, List<(double start, double end)> silences)> AnalyseSilenceAsync(
        string ffmpegPath,
        string inputPath,
        double thresholdDb,
        double minDurationSec)
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
        string filter = string.Create(
            CultureInfo.InvariantCulture,
            $"silencedetect=noise={thresholdDb}dB:duration={minDurationSec}");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("-af");
        startInfo.ArgumentList.Add(filter);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("null");
        startInfo.ArgumentList.Add("-");

        using var process = new Process { StartInfo = startInfo };
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = false;
        process.EnableRaisingEvents = true;

        var errorLines = new StringBuilder();
        var silences = new List<(double start, double end)>();
        TimeSpan inputDuration = TimeSpan.Zero;
        double? activeSilenceStart = null;

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data == null)
                return;

            errorLines.AppendLine(e.Data);

            var durationMatch = Regex.Match(e.Data, @"Duration:\s*(\d+):(\d+):(\d+\.\d+)");
            if (durationMatch.Success)
            {
                int h = int.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                int m = int.Parse(durationMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                double sec = double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                inputDuration = new TimeSpan(h, m, 0) + TimeSpan.FromSeconds(sec);
            }

            var startMatch = Regex.Match(e.Data, @"silence_start:\s*([0-9.]+)");
            if (startMatch.Success)
                activeSilenceStart = double.Parse(startMatch.Groups[1].Value, CultureInfo.InvariantCulture);

            var endMatch = Regex.Match(e.Data, @"silence_end:\s*([0-9.]+)");
            if (endMatch.Success && activeSilenceStart.HasValue)
            {
                double end = double.Parse(endMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                if (end > activeSilenceStart.Value)
                    silences.Add((activeSilenceStart.Value, end));
                activeSilenceStart = null;
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            return (false, errorLines.ToString().Trim(), TimeSpan.Zero, silences);

        if (activeSilenceStart.HasValue && inputDuration > TimeSpan.Zero)
            silences.Add((activeSilenceStart.Value, inputDuration.TotalSeconds));

        double silenceSeconds = silences.Sum(s => Math.Max(0, s.end - s.start));
        var estimatedDuration = TimeSpan.FromSeconds(Math.Max(0, inputDuration.TotalSeconds - silenceSeconds));
        return (true, "", estimatedDuration, silences);
#else
        await Task.CompletedTask;
        return (false, "FFmpeg silence analysis is only available on Windows.", TimeSpan.Zero, new List<(double start, double end)>());
#endif
    }

    private static List<(double start, double end)> BuildKeepSegments(
        List<(double start, double end)> silences,
        double totalDurationSec,
        double truncateToSec)
    {
        if (totalDurationSec <= 0)
            return new List<(double start, double end)>();

        if (silences.Count == 0)
            return new List<(double start, double end)> { (0, totalDurationSec) };

        var orderedSilences = silences
            .Where(s => s.end > s.start)
            .OrderBy(s => s.start)
            .ToList();
        var segments = new List<(double start, double end)>();

        for (int i = 0; i <= orderedSilences.Count; i++)
        {
            double speechStart = i == 0
                ? 0
                : orderedSilences[i - 1].end - truncateToSec;
            double speechEnd = i == orderedSilences.Count
                ? totalDurationSec
                : orderedSilences[i].start + truncateToSec;

            speechStart = Math.Clamp(speechStart, 0, totalDurationSec);
            speechEnd = Math.Clamp(speechEnd, 0, totalDurationSec);
            if (speechEnd > speechStart)
                segments.Add((speechStart, speechEnd));
        }

        return MergeSegments(segments, totalDurationSec);
    }

    private static List<(double start, double end)> MergeSegments(
        List<(double start, double end)> segments,
        double totalDurationSec)
    {
        var merged = new List<(double start, double end)>();
        foreach (var segment in segments.OrderBy(s => s.start))
        {
            if (merged.Count == 0 || segment.start > merged[^1].end)
            {
                merged.Add(segment);
                continue;
            }

            var last = merged[^1];
            merged[^1] = (last.start, Math.Min(totalDurationSec, Math.Max(last.end, segment.end)));
        }

        return merged;
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
