using System.Windows.Controls;
using VideoEditorWPF.Interfaces;

namespace VideoEditorWPF.Services
{
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
