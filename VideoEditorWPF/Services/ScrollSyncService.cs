using System.Windows.Controls;

namespace VideoEditorWPF.Services
{
    public interface IScrollSyncService
    {
        void SyncVerticalScroll(double offset, params ScrollViewer[] viewers);
        void SyncHorizontalScroll(double offset, params ScrollViewer[] viewers);
    }

    public class ScrollSyncService : IScrollSyncService
    {
        public void SyncVerticalScroll(double offset, params ScrollViewer[] viewers)
        {
            foreach (var viewer in viewers)
            {
                if (viewer != null)
                {
                    viewer.ScrollToVerticalOffset(offset);
                }
            }
        }

        public void SyncHorizontalScroll(double offset, params ScrollViewer[] viewers)
        {
            foreach (var viewer in viewers)
            {
                if (viewer != null)
                {
                    viewer.ScrollToHorizontalOffset(offset);
                }
            }
        }
    }
}
