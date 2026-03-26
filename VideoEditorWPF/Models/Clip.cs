using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace VideoEditorWPF.Models
{
    public class Clip
    {
        public string FilePath { get; set; }
        public double OffsetSeconds { get; set; }
        public double DurationSeconds { get; set; }
        public bool IsVideoClip { get; set; }
        public int TrackIndex { get; set; }

        //public double StartX { get; set; }
        //public double Width { get; set; }

        public double GetOffsetPixels(double timelineScale) => OffsetSeconds * timelineScale;
        public double GetWidth(double timelineScale) => DurationSeconds * timelineScale;
    }
    public class ClipDragInfo
    {
        public Clip Clip { get; set; }
        public Rectangle Visual { get; set; }
        public TextBlock Label { get; set; }
        public Point LastPosition { get; set; }
    }
}
