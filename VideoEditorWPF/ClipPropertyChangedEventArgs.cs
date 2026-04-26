using System;
using VideoEditorWPF.Models;

namespace VideoEditorWPF
{
    public class ClipPropertyChangedEventArgs : EventArgs
    {
        public Clip Clip { get; }
        public string PropertyName { get; }

        public ClipPropertyChangedEventArgs(Clip clip, string propertyName)
        {
            Clip = clip;
            PropertyName = propertyName;
        }
    }
}