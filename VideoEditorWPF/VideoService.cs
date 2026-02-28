using OpenCvSharp;
using System.IO;
using System.Windows.Media.Imaging;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Services
{
    public class VideoService
    {
        public async Task<VideoClip> LoadVideoAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return new VideoClip();

            using var capture = new VideoCapture(filePath);
            if (!capture.IsOpened())
                return new VideoClip();

            var frameCount = capture.FrameCount;
            var fps = capture.Fps;
            var duration = frameCount > 0 && fps > 0 ? frameCount / fps : 0;

            var clip = new VideoClip
            {
                FilePath = filePath,
                Duration = duration,
                TrimEnd = duration
            };

            return clip;
        }

        public BitmapImage? GetPreviewFrame(VideoClip clip, double timeSeconds)
        {
            if (clip?.FilePath == null || !File.Exists(clip.FilePath))
                return null;

            using var capture = new VideoCapture(clip.FilePath);
            if (!capture.IsOpened())
                return null;

            var fps = capture.Fps;
            if (fps <= 0) return null;

            capture.Set(VideoCaptureProperties.PosFrames, (int)(timeSeconds * fps));

            using var frame = new Mat();
            if (!capture.Read(frame) || frame.Empty())
                return null;

            return MatToBitmapImage(frame);
        }

        private BitmapImage MatToBitmapImage(Mat mat)
        {
            using var frameRGB = new Mat();
            Cv2.CvtColor(mat, frameRGB, ColorConversionCodes.BGR2RGB);

            byte[] data;
            Cv2.ImEncode(".png", frameRGB, out data);

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = new MemoryStream(data);
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

    }
}
