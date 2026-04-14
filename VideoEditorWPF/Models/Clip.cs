using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace VideoEditorWPF.Models
{
    public partial class Clip : INotifyPropertyChanged
    {
        private double _offsetSeconds;      // Позиция на таймлайне
        private double _durationSeconds;    // Длительность на таймлайне

        private double _trimStart = 0;      // Начало обрезки внутри исходного файла (сек)
        private double _trimEnd = 0;        // Конец обрезки внутри исходного файла (сек)

        /// <summary>
        /// Начало обрезки внутри исходного файла (секунды от начала файла)
        /// </summary>
        public double TrimStart
        {
            get => _trimStart;
            set
            {
                if (Math.Abs(_trimStart - value) > 0.001)
                {
                    _trimStart = Math.Max(0, Math.Min(value, SourceDuration - MinTrimDuration));
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TrimmedDuration));
                    OnPropertyChanged(nameof(TrimEnd));
                }
            }
        }

        public double DurationSeconds
        {
            get => _durationSeconds;
            set
            {
                if (!(_durationSeconds == value))
                {
                    _durationSeconds = value;
                    OnPropertyChanged();
                }
            }
        }

        public double OffsetSeconds
        {
            get => _offsetSeconds;
            set
            {
                if (!(_offsetSeconds == value))
                {
                    _offsetSeconds = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Конец обрезки внутри исходного файла (секунды от начала файла)
        /// </summary>
        public double TrimEnd
        {
            get => _trimEnd;
            set
            {
                if (Math.Abs(_trimEnd - value) > 0.001)
                {
                    _trimEnd = Math.Max(TrimStart + MinTrimDuration, Math.Min(value, SourceDuration));
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TrimmedDuration));
                    OnPropertyChanged(nameof(TrimStart));
                }
            }
        }

        /// <summary>
        /// Длительность после обрезки (на таймлайне = TrimmedDuration)
        /// </summary>
        public double TrimmedDuration => _trimEnd - _trimStart;

        /// <summary>
        /// Исходная длительность файла
        /// </summary>
        public double SourceDuration { get; set; }

        /// <summary>
        /// Минимальная длительность обрезки (0.1 сек)
        /// </summary>
        public double MinTrimDuration => 0.1;

        /// <summary>
        /// Обновляет длительность клипа на таймлайне в соответствии с обрезкой
        /// </summary>
        public void ApplyTrim()
        {
            DurationSeconds = TrimmedDuration;
        }

        /// <summary>
        /// Сброс обрезки до полного файла
        /// </summary>
        public void ResetTrim()
        {
            TrimStart = 0;
            TrimEnd = SourceDuration;
            DurationSeconds = SourceDuration;
        }

        /// <summary>
        /// Получить параметры для FFmpeg (trim filter)
        /// </summary>
        public string GetFFmpegTrimFilter()
        {
            if (TrimStart <= 0 && TrimEnd >= SourceDuration)
                return string.Empty;

            return $"trim=start={TrimStart}:end={TrimEnd},setpts=PTS-STARTPTS";
        }

        public double TotalDurationSecondsFromMediaFile
        {
            get
            {
                return System.IO.File.Exists(FilePath)
                    ? VideoEditorWPF.Models.MediaFile.GetDurationFFmpegAsync(FilePath).GetAwaiter().GetResult()
                    : 30.0;
            }
        }

        private double _start = 0;  // секунда начала внутри видео
        private double _duration = 0;  // длительность внутри видео

        public double Start
        {
            get => _start;
            set
            {
                if (!(_start == value))
                {
                    _start = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Duration
        {
            get => _duration;
            set
            {
                if (!(_duration == value))
                {
                    _duration = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FilePath { get; set; }
        public bool IsVideoClip { get; set; }
        public int TrackIndex { get; set; }

        public string ClipId { get; set; } = System.Guid.NewGuid().ToString();
        public int InstanceNumber { get; set; } = 1;


        public double GetOffsetPixels(double timelineScale)
        {
            double result = OffsetSeconds * timelineScale;
            Debug.WriteLine($"Clip.GetOffsetPixels: OffsetSeconds = {OffsetSeconds:F3}, timelineScale = {timelineScale:F3} = {result:F3}");
            return result;
        }

        public double GetWidth(double timelineScale)
        {
            double result = DurationSeconds * timelineScale;
            Debug.WriteLine($"Clip.GetWidth: DurationSeconds = {DurationSeconds:F3}, timelineScale = {timelineScale:F3} = {result:F3}");
            return result;
        }

        public string DisplayName =>
            System.IO.Path.GetFileNameWithoutExtension(FilePath) +
            (InstanceNumber > 1 ? $"_{InstanceNumber}" : "");

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class ClipDragInfo
    {
        public Clip Clip { get; set; }
        public Rectangle Visual { get; set; }
        public TextBlock Label { get; set; }
        public Point LastPosition { get; set; }
    }
}
