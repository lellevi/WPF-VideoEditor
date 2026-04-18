// VideoEditorWPF/Services/ExportService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoEditorWPF.Models;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Services
{
    public interface IExportService
    {
        Task<bool> ExportVideoAsync(TimelineViewModel timeline, string outputPath, IProgress<double> progress);
        bool CanExport(TimelineViewModel timeline);
    }

    public class ExportService : IExportService
    {
        private readonly IDialogService _dialogService;
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public ExportService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public bool CanExport(TimelineViewModel timeline)
        {
            return timeline != null && timeline.Tracks.Any(t => t.Clips.Any());
        }

        public async Task<bool> ExportVideoAsync(TimelineViewModel timeline, string outputPath, IProgress<double> progress)
        {
            if (!CanExport(timeline))
            {
                return false;
            }

            try
            {
                double totalDuration = timeline.GetTotalDuration().TotalSeconds;
                string ffmpegPath = GetFfmpegPath();

                if (!File.Exists(ffmpegPath))
                {
                    _dialogService?.ShowMessage($"FFmpeg not found at: {ffmpegPath}", "FFmpeg Missing");
                    return false;
                }

                // Получаем все клипы, отсортированные по времени
                var allClips = timeline.Tracks
                    .SelectMany(t => t.Clips)
                    .OrderBy(c => c.OffsetSeconds)
                    .ToList();

                if (!allClips.Any())
                {
                    _dialogService?.ShowMessage("No clips to export", "Export Error");
                    return false;
                }

                // Создаем временную папку
                string tempDir = Path.Combine(Path.GetTempPath(), $"VideoExport_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    progress?.Report(10);

                    // Шаг 1: Обрезаем и подготавливаем каждый клип
                    var preparedFiles = new List<string>();
                    double currentPosition = 0;

                    for (int i = 0; i < allClips.Count; i++)
                    {
                        var clip = allClips[i];

                        // Проверяем есть ли промежуток перед этим клипом
                        if (clip.OffsetSeconds > currentPosition + 0.01)
                        {
                            double gapDuration = clip.OffsetSeconds - currentPosition;
                            string silenceFile = Path.Combine(tempDir, $"silence_{i}.mp4");

                            // Создаем черный экран с тишиной для промежутка
                            bool silenceCreated = await CreateSilenceVideoAsync(ffmpegPath, gapDuration, silenceFile);
                            if (silenceCreated)
                            {
                                preparedFiles.Add(silenceFile);
                            }
                        }

                        // Обрезаем клип
                        string trimmedFile = Path.Combine(tempDir, $"clip_{i:000}.mp4");
                        bool success = await TrimClipAsync(ffmpegPath, clip, trimmedFile);
                        if (!success)
                        {
                            _dialogService?.ShowMessage($"Failed to process clip: {clip.DisplayName}", "Export Error");
                            return false;
                        }

                        preparedFiles.Add(trimmedFile);
                        currentPosition = clip.OffsetSeconds + clip.DurationSeconds;

                        progress?.Report(10 + (i + 1) * 60.0 / allClips.Count);
                    }

                    // Шаг 2: Склеиваем все подготовленные файлы
                    if (!preparedFiles.Any())
                    {
                        _dialogService?.ShowMessage("No files to concatenate", "Export Error");
                        return false;
                    }

                    progress?.Report(75);

                    // Используем concat demuxer с правильным форматом
                    bool concatSuccess = await ConcatFilesAsync(ffmpegPath, preparedFiles, outputPath);

                    if (!concatSuccess)
                    {
                        // Если concat не работает, пробуем альтернативный метод
                        concatSuccess = await ConcatWithFFmpegAsync(ffmpegPath, preparedFiles, outputPath);
                    }

                    if (!concatSuccess)
                    {
                        _dialogService?.ShowMessage("Failed to concatenate clips", "Export Error");
                        return false;
                    }

                    progress?.Report(100);

                    return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
                }
                finally
                {
                    // Очищаем временные файлы
                    if (Directory.Exists(tempDir))
                    {
                        try { Directory.Delete(tempDir, true); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Export exception: {ex.Message}");
                _dialogService?.ShowMessage($"Export error: {ex.Message}", "Error");
                return false;
            }
        }

        private async Task<bool> CreateSilenceVideoAsync(string ffmpegPath, double duration, string outputPath)
        {
            if (duration <= 0) return false;

            // Определяем размер из первого клипа или используем стандартный
            string size = "1024x576";

            string args = $"-f lavfi -i color=c=black:s={size}:d={duration.ToString("F6", InvariantCulture)} " +
                         $"-f lavfi -i anullsrc=r=44100:cl=stereo:d={duration.ToString("F6", InvariantCulture)} " +
                         $"-c:v libx264 -preset ultrafast -crf 30 " +
                         $"-c:a aac -b:a 128k " +
                         $"-pix_fmt yuv420p " +
                         $"-shortest " +
                         $"-y \"{outputPath}\"";

            return await RunFFmpegAsync(ffmpegPath, args);
        }

        private async Task<bool> TrimClipAsync(string ffmpegPath, Clip clip, string outputPath)
        {
            double duration = clip.TrimmedDuration;
            double start = clip.TrimStart;

            string args = $"-i \"{clip.FilePath}\" " +
                         $"-ss {start.ToString("F6", InvariantCulture)} " +
                         $"-t {duration.ToString("F6", InvariantCulture)} " +
                         $"-c:v libx264 -preset fast -crf 23 " +
                         $"-c:a aac -b:a 128k " +
                         $"-pix_fmt yuv420p " +
                         $"-avoid_negative_ts make_zero " +
                         $"-y \"{outputPath}\"";

            return await RunFFmpegAsync(ffmpegPath, args);
        }

        private async Task<bool> ConcatFilesAsync(string ffmpegPath, List<string> files, string outputPath)
        {
            // Метод 1: Используем concat demuxer с файлом списка
            string tempDir = Path.GetDirectoryName(files[0]);
            string concatFile = Path.Combine(tempDir, "concat_list.txt");

            // Нормализуем пути для FFmpeg (используем обратные слеши или экранируем)
            using (var writer = new StreamWriter(concatFile, false, Encoding.UTF8))
            {
                foreach (var file in files)
                {
                    // Используем абсолютные пути с правильными слешами
                    string normalizedPath = Path.GetFullPath(file).Replace("\\", "/");
                    writer.WriteLine($"file '{normalizedPath}'");
                }
            }

            // Проверяем содержимое файла
            string content = File.ReadAllText(concatFile);
            Debug.WriteLine($"Concat file content:\n{content}");

            string args = $"-f concat -safe 0 -i \"{concatFile}\" -c copy -y \"{outputPath}\"";

            return await RunFFmpegAsync(ffmpegPath, args);
        }

        private async Task<bool> ConcatWithFFmpegAsync(string ffmpegPath, List<string> files, string outputPath)
        {
            // Метод 2: Используем filter_complex concat
            var inputs = new StringBuilder();
            var filterInputs = new StringBuilder();

            for (int i = 0; i < files.Count; i++)
            {
                inputs.Append($"-i \"{files[i]}\" ");
                filterInputs.Append($"[{i}:v][{i}:a]");
            }

            string args = $"{inputs} " +
                         $"-filter_complex \"{filterInputs} concat=n={files.Count}:v=1:a=1 [v][a]\" " +
                         $"-map \"[v]\" -map \"[a]\" " +
                         $"-c:v libx264 -preset fast -crf 23 " +
                         $"-c:a aac -b:a 128k " +
                         $"-y \"{outputPath}\"";

            return await RunFFmpegAsync(ffmpegPath, args);
        }

        private async Task<bool> RunFFmpegAsync(string ffmpegPath, string arguments)
        {
            try
            {
                Debug.WriteLine($"Running FFmpeg: {arguments}");

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = psi };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"FFmpeg out: {e.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"FFmpeg err: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine($"FFmpeg exit code: {process.ExitCode}");
                    Debug.WriteLine($"FFmpeg error: {errorBuilder}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RunFFmpegAsync exception: {ex.Message}");
                return false;
            }
        }

        private string GetFfmpegPath()
        {
            string[] possiblePaths = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
                @"C:\ffmpeg\bin\ffmpeg.exe",
                "ffmpeg.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.WriteLine($"Found FFmpeg at: {path}");
                    return path;
                }
            }

            return "ffmpeg.exe";
        }
    }
}