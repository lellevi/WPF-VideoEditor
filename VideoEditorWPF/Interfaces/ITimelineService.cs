using System.Threading.Tasks;
using VideoEditorWPF.Models;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Interfaces
{
    public interface ITimelineService
    {
        Task AddClipToTimelineAsync(MediaFile mediaFile, TimelineViewModel timeline);
        double GetTimelineLength(TimelineViewModel timeline);
    }
}
