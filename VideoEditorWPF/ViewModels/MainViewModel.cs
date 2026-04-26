using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using VideoEditorWPF.Commands;
using VideoEditorWPF.Interfaces;
using VideoEditorWPF.Models;
using VideoEditorWPF.Services;

namespace VideoEditorWPF.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IMediaService _mediaService;
        private readonly IDialogService _dialogService;
        private readonly ITimelineService _timelineService;
        private readonly IExportService _exportService;
        public ObservableCollection<MediaFile> MediaFiles { get; } = new();
        public TimelineViewModel Timeline { get; }
        public PreviewViewModel Preview { get; }
        public ICommand AddMediaCommand { get; }
        public ICommand ResetPlayheadCommand { get; }
        public ICommand ExportCommand { get; }

        private bool _isExporting;
        public bool IsNotExporting => !IsExporting;
        private double _exportProgress;

        public MainViewModel(IMediaService mediaService, IDialogService dialogService, ITimelineService timelineService,
            TimelineViewModel timelineViewModel, PreviewViewModel previewViewModel, IExportService exportService = null)
        {
            _mediaService = mediaService;
            _dialogService = dialogService;
            _timelineService = timelineService;
            _exportService = exportService ?? new ExportService(dialogService);
            Timeline = timelineViewModel;
            Preview = previewViewModel;
            AddMediaCommand = new RelayCommand(ExecuteAddMedia);
            ResetPlayheadCommand = new RelayCommand(ExecuteResetPlayhead);
            ExportCommand = new RelayCommand(ExecuteExport, _ => CanExport());
        }
        public bool IsExporting
        {
            get => _isExporting;
            set
            {
                _isExporting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotExporting));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public double ExportProgress
        {
            get => _exportProgress;
            set
            {
                _exportProgress = value;
                OnPropertyChanged();
            }
        }

        private string _exportStatus;
        public string ExportStatus
        {
            get => _exportStatus;
            set
            {
                _exportStatus = value;
                OnPropertyChanged();
            }
        }
        private void ExecuteResetPlayhead(object parameter)
        {
            Timeline.PlayheadPosition = 0;
            Preview.Reset();
        }
        private async void ExecuteAddMedia(object parameter)
        {
            var fileNames = _dialogService.ShowOpenFileDialog("Media Files|*.mp4;*.avi;*.mkv;*.mp3;*.wav;*.jpg;*.png",multiselect: true);

            if (fileNames == null || fileNames.Length == 0)
                return;

            foreach (string filePath in fileNames)
            {
                try
                {
                    var mediaFile = new MediaFile(filePath);

                    MediaFiles.Add(mediaFile);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка");
                }
            }

            Preview.UpdateTotalDuration();

            _ = Task.Delay(1000).ContinueWith(_ => GenerateThumbnailsAsync());
        }
        private bool CanExport()
        {
            return !IsExporting && _exportService.CanExport(Timeline);
        }
        private async Task ExportVideoAsync()
        {
            string outputPath = _dialogService.ShowSaveFileDialog("Export Video", "MP4 files|*.mp4");
            if (string.IsNullOrEmpty(outputPath))
                return;

            IsExporting = true;
            ExportProgress = 0;
            ExportStatus = "Preparing export...";

            var progress = new Progress<double>(value =>
            {
                ExportProgress = value;
                ExportStatus = $"Exporting... {value:F1}%";
            });

            try
            {
                ExportStatus = "Exporting video...";
                bool success = await _exportService.ExportVideoAsync(Timeline, outputPath, progress);

                if (success)
                {
                    ExportProgress = 100;
                    ExportStatus = "Export completed!";
                    _dialogService.ShowMessage("Export completed successfully!", "Success");
                }
                else
                {
                    ExportStatus = "Export failed!";
                    _dialogService.ShowMessage("Export failed. Check logs for details.", "Error");
                }
            }
            catch (Exception ex)
            {
                ExportStatus = $"Error: {ex.Message}";
                _dialogService.ShowMessage($"Export error: {ex.Message}", "Error");
            }
            finally
            {
                IsExporting = false;
            }
        }
        private async void GenerateThumbnailsAsync()
        {
            foreach (MediaFile mediaFile in MediaFiles.ToList())
            {
                if (string.IsNullOrEmpty(mediaFile.ThumbnailPath))
                {
                    try
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            mediaFile.ThumbnailPath = "pack://application:,,,/Resources/placeholder.png";
                        });
                    }
                    catch { }
                }
            }
        }
        private async void ExecuteExport(object parameter)
        {
            string outputPath = _dialogService.ShowSaveFileDialog("MP4 files|*.mp4", "output.mp4");
            if (string.IsNullOrEmpty(outputPath))
                return;

            IsExporting = true;
            ExportProgress = 0;
            ExportStatus = "Preparing export...";

            var progress = new Progress<double>(value =>
            {
                ExportProgress = value;
                ExportStatus = $"Exporting... {value:F1}%";
            });

            try
            {
                ExportStatus = "Exporting video...";
                bool success = await _exportService.ExportVideoAsync(Timeline, outputPath, progress);

                if (success)
                {
                    ExportProgress = 100;
                    ExportStatus = "Export completed!";
                    _dialogService.ShowMessage("Export completed successfully!", "Success");
                }
                else
                {
                    ExportStatus = "Export failed!";
                    _dialogService.ShowMessage("Export failed. Check logs for details.", "Error");
                }
            }
            catch (Exception ex)
            {
                ExportStatus = $"Error: {ex.Message}";
                _dialogService.ShowMessage($"Export error: {ex.Message}", "Error");
            }
            finally
            {
                IsExporting = false;
            }
        }
    }
}