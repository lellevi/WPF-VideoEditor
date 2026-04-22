using System.Collections.Generic;

namespace VideoEditorWPF.Services
{
    public class FrameCache
    {
        public Dictionary<int, byte[]> Frames { get; } = new Dictionary<int, byte[]>();
    }
}
