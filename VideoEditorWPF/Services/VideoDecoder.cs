using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Drawing = System.Drawing;

namespace VideoEditorWPF.Services
{
    public class VideoDecoder : IDisposable
    {
        private readonly string _filePath;
        public int Width { get; } = 640;
        public int Height { get; } = 360;

        public VideoDecoder(string filePath)
        {
            _filePath = filePath;
        }
        public async Task<byte[]> GetFrameAsync(string filePath, double timeInSeconds)
        {
            try
            {
                var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");
                if (!File.Exists(ffmpegPath)) return null;
                var tempPng = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid():N}.png");
                var timeStr = timeInSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                var args = $"-ss {timeStr} -i \"{filePath}\" -frames:v 1 -vf \"scale={Width}:{Height}\" -pix_fmt bgra -y \"{tempPng}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && File.Exists(tempPng))
                {
                    var fi = new FileInfo(tempPng);
                    using var fs = new FileStream(tempPng, FileMode.Open, FileAccess.Read);
                    using var bitmap = new Drawing.Bitmap(fs);

                    var result = BitmapToBytes(bitmap);
                    return result;
                }
            }
            catch (ArgumentException ex) { throw new ArgumentException($"GetFrame error: {ex.Message}"); }
            return null;
        }

        private byte[] BitmapToBytes(Drawing.Bitmap bitmap)
        {
            var rect = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[Math.Abs(data.Stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        public void Dispose()
        {
        }
    }
}
