using System;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Factories
{
    public interface IClipFactory
    {
        Clip CreateClip(MediaFile mediaFile, double startTimeSeconds, double timelineScale, int trackIndex);
    }

    public class ClipFactory : IClipFactory
    {
        public Clip CreateClip(MediaFile mediaFile, double startTimeSeconds, double timelineScale, int trackIndex)
        {
            return new Clip
            {
                FilePath = mediaFile.FilePath,
                IsVideoClip = mediaFile.FilePath.EndsWith(".mp4") || mediaFile.FilePath.EndsWith(".avi") || mediaFile.FilePath.EndsWith(".mkv"),
                OffsetSeconds = startTimeSeconds,
                DurationSeconds = (float)mediaFile.Duration.TotalSeconds,  // ← Из MediaFile!

                TrackIndex = trackIndex
            };
        }

    }
}
// Фабрика для создания клипов видеоредактора.
// Преобразует MediaFile в Clip с расчетом позиции (StartX) и ширины (Width) по timelineScale.
// Автоматически определяет тип клипа (видео/аудио) по расширению файла.
// Устанавливает начальную позицию, длительность и трек для timeline.
