using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoEditorWPF.Models
{
    public partial class Clip : INotifyPropertyChanged
    {
        private double _offsetSeconds;      // Позиция на таймлайне
        private double _durationSeconds;    // Длительность на таймлайне

        private double _trimStart = 0;      // Начало обрезки внутри исходного файла
        private double _trimEnd = 0;        // Конец обрезки внутри исходного файла
        public string FilePath { get; set; }
        public bool IsVideoClip { get; set; }
        public int TrackIndex { get; set; }
        public string ClipId { get; set; } = System.Guid.NewGuid().ToString();
        public int InstanceNumber { get; set; } = 1;
        public double TrimmedDuration => TrimEnd - TrimStart;
        public double SourceDuration { get; set; }
        public double MinTrimDuration => 0.1;

        private double _start = 0;  // секунда начала внутри видео
        private double _duration = 0;  // длительность внутри видео
        public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(FilePath) + (InstanceNumber > 1 ? $"_{InstanceNumber}" : "");

        public double TrimStart
        {
            get => _trimStart;
            set
            {
                if (Math.Abs(_trimStart - value) > 0.001)
                {
                    _trimStart = Math.Max(0, Math.Min(value, TrimEnd - MinTrimDuration));
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TrimmedDuration));
                }
            }
        }
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

        public void ApplyTrim()
        {
            DurationSeconds = TrimmedDuration;
        }
        public void ResetTrim()
        {
            TrimStart = 0;
            TrimEnd = SourceDuration;
            DurationSeconds = SourceDuration;
        }
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

        public double GetOffsetPixels(double timelineScale)
        {
            double result = OffsetSeconds * timelineScale;
            return result;
        }

        public double GetWidth(double timelineScale)
        {
            double result = DurationSeconds * timelineScale;
            return result;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
