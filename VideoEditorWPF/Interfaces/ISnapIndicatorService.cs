using System.Windows.Controls;

namespace VideoEditorWPF.Interfaces
{
    public interface ISnapIndicatorService
    {
        void Show(Canvas canvas, double position, double scale, double height);
        void Hide();
    }
}
