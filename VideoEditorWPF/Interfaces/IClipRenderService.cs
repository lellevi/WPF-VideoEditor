using System.Collections.Generic;
using System.Windows.Controls;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Interfaces
{
    public interface IClipRenderService
    {
        void RenderClips(Canvas canvas, IEnumerable<Clip> clips, double trackTop, double timelineScale);
    }
}