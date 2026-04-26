using VideoEditorWPF.Models;

namespace VideoEditorWPF.Interfaces
{
    public interface IClipFactory
    {
        Clip CreateClip(MediaFile mediaFile, double startTimeSeconds, double timelineScale, int trackIndex);
    }
}
