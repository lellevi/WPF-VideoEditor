using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Interfaces
{
    public interface IPreviewRenderService
    {
        WriteableBitmap InitializePreview(int width = 640, int height = 360);
        Task UpdatePreview(WriteableBitmap bitmap, TimeSpan currentTime, IEnumerable<Track> tracks);
        void SetPreviewFPS(int fps);
        void PreloadVideoFrames(string filePath);
    }
}
