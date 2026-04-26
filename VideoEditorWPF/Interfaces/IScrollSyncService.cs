using System.Windows.Controls;

namespace VideoEditorWPF.Interfaces
{
    public interface IScrollSyncService
    {
        void SyncVerticalScroll(double offset, params ScrollViewer[] viewers);
        void SyncHorizontalScroll(double offset, params ScrollViewer[] viewers);
    }
}
