using System.Collections.Generic;
using System.Windows.Controls;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Interfaces
{
    public interface ITrackRenderService
    {
        void RenderTracks(Canvas canvas, IEnumerable<Track> tracks, double canvasWidth, double timelineScale);
        void ClearTracks(Canvas canvas);
        void ForceRefresh(Canvas canvas, IEnumerable<Track> tracks, double canvasWidth, double timelineScale);
    }
}