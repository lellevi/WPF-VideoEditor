using System;
using System.Threading.Tasks;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Interfaces
{
    public interface IExportService
    {
        Task<bool> ExportVideoAsync(TimelineViewModel timeline, string outputPath, IProgress<double> progress);
        bool CanExport(TimelineViewModel timeline);
    }
}