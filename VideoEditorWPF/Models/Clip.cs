namespace VideoEditorWPF.Models
{
    public class Clip
    {
        public string FilePath { get; set; }
        public double OffsetPixels { get; set; }
        public double Width { get; set; }
        public bool IsVideoClip { get; set; }
        public int TrackIndex { get; set; }
        public double OffsetSeconds { get; set; }
        public double DurationSeconds { get; set; }
    }
}
