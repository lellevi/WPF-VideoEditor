using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VideoEditorWPF.Models;


namespace VideoEditorWPF.Services
{
    public interface IMediaService
    {
        MediaFile LoadMediaFile(string filePath);
        List<MediaFile> LoadMultipleMediaFiles(string[] filePaths);
    }

    public class MediaService : IMediaService
    {
        private readonly Random _random = new Random();

        public MediaFile LoadMediaFile(string filePath)
        {
            var mediaFile = new MediaFile
            {
                FilePath = filePath,
                ThumbnailPath = GenerateThumbnail(filePath),
                Duration = TimeSpan.FromSeconds(10 + _random.Next(20)) // TODO: FFMPEG
            };
            return mediaFile;
        }

        public List<MediaFile> LoadMultipleMediaFiles(string[] filePaths)
        {
            return filePaths.Select(LoadMediaFile).ToList();
        }

        private string GenerateThumbnail(string filePath) // TODO: FFMPEG
        {
            string thumbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"thumb_{Guid.NewGuid()}.png");
            var bitmap = new RenderTargetBitmap(80, 60, 96, 96, PixelFormats.Pbgra32);
            var rect = new Rectangle { Fill = new SolidColorBrush(Colors.DodgerBlue), RadiusX = 5, RadiusY = 5 };
            bitmap.Render(rect);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(thumbPath))
                encoder.Save(stream);
            return thumbPath;
        }
    }
}
