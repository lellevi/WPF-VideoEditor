using System;

namespace VideoEditorWPF.Models
{
    public class MediaFile
    {
        public string FilePath { get; set; }
        public string FileName => System.IO.Path.GetFileName(FilePath);
        public string ThumbnailPath { get; set; }
        public TimeSpan Duration { get; set; }
        public string DurationString => Duration.ToString(@"hh\:mm\:ss");
    }
}
