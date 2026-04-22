using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace VideoEditorWPF.Models
{
    public class ClipDragInfo
    {
        public Clip Clip { get; set; }
        public Rectangle Visual { get; set; }
        public TextBlock Label { get; set; }
        public Point LastPosition { get; set; }
    }
}
