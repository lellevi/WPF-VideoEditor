using System;
using System.Linq;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Factories
{
    public interface IClipFactory
    {
        Clip CreateClip(MediaFile mediaFile, double startTimeSeconds, double timelineScale, int trackIndex);
    }

    public class ClipFactory : IClipFactory
    {
        public Clip CreateClip(MediaFile mediaFile, double offsetSeconds, double timelineScale, int trackIndex)
        {
            return new Clip
            {
                FilePath = mediaFile.FilePath,
                OffsetSeconds = offsetSeconds,
                SourceDuration = mediaFile.Duration.TotalSeconds,
                TrimStart = 0,
                TrimEnd = mediaFile.Duration.TotalSeconds,
                DurationSeconds = mediaFile.Duration.TotalSeconds, // Изначально полная длительность
                IsVideoClip = IsVideoFile(mediaFile.FilePath),
                TrackIndex = trackIndex,
                ClipId = Guid.NewGuid().ToString(),
                InstanceNumber = 1
            };
        }
        private bool IsVideoFile(string filePath)
        {
            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            string[] videoExts = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
            return videoExts.Contains(ext);
        }
    }
}
// Фабрика для создания клипов видеоредактора.
// Преобразует MediaFile в Clip с расчетом позиции (StartX) и ширины (Width) по timelineScale.
// Автоматически определяет тип клипа (видео/аудио) по расширению файла.
// Устанавливает начальную позицию, длительность и трек для timeline.
