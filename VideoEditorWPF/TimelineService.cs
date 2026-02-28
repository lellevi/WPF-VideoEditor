using System.IO;
using VideoEditorWPF.Models;
using Xabe.FFmpeg;

namespace VideoEditorWPF
{
    public class TimelineService
    {
        public async Task ExportTimelineAsync(List<VideoClip> clips, string outputPath, Action<double> progressCallback)
        {
            var tempFiles = new List<string>();

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var tempClip = Path.GetTempFileName() + ".mp4";
                tempFiles.Add(tempClip);

                var startTime = clip.TrimStart.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                var duration = (clip.TrimEnd - clip.TrimStart).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                var conversion = FFmpeg.Conversions.New()
                    .AddParameter($"-ss {startTime}")
                    .AddParameter($"-i \"{clip.FilePath}\"")
                    .AddParameter($"-t {duration}")
                    .AddParameter("-c copy")
                    .AddParameter("-avoid_negative_ts make_zero")
                    .SetOutput(tempClip);

                await conversion.Start();
                progressCallback((i + 1.0) / clips.Count * 50);
            }

            var listFile = Path.GetTempFileName() + ".txt";
            File.WriteAllLines(listFile, tempFiles.Select(f => $"file '{Path.GetFullPath(f).Replace("'", "'\\''")}'"));

            var concatConversion = FFmpeg.Conversions.New()
                .AddParameter("-f concat")
                .AddParameter("-safe 0")
                .AddParameter($"-i \"{listFile}\"")
                .AddParameter("-c copy")
                .SetOutput(outputPath);

            await concatConversion.Start();

            File.Delete(listFile);
            foreach (var temp in tempFiles.Where(File.Exists))
                File.Delete(temp);

            progressCallback(100);
        }

    }
}
