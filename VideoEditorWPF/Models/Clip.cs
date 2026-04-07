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

        public double TotalDurationSecondsFromMediaFile
        {
            get
            {
                return System.IO.File.Exists(FilePath)
                    ? VideoEditorWPF.Models.MediaFile.GetDurationFFmpegAsync(FilePath).GetAwaiter().GetResult()
                    : 30.0;
            }
        }
        private double _offsetSeconds;
        private double _durationSeconds;

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
