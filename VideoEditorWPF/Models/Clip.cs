namespace VideoEditorWPF.Models
{
    public class Clip
    {
        public string FilePath { get; set; }
        public double OffsetSeconds { get; set; }
        public double DurationSeconds { get; set; }
        public bool IsVideoClip { get; set; }
        public int TrackIndex { get; set; }

        public double GetOffsetPixels(double timelineScale) => OffsetSeconds * timelineScale;
        public double GetWidth(double timelineScale) => DurationSeconds * timelineScale;
    }
}
