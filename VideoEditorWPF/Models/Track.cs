using System.Collections.ObjectModel;

namespace VideoEditorWPF.Models
{
    public class Track
    {
        public string Name { get; set; }
        public TrackType Type { get; set; }
        public ObservableCollection<Clip> Clips { get; set; }

        public Track()
        {
            Clips = new ObservableCollection<Clip>();
        }
    }
}
