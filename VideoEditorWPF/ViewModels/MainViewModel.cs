using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using VideoEditorWPF.Commands;
using VideoEditorWPF.Models;
using VideoEditorWPF.Services;

namespace VideoEditorWPF.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IMediaService _mediaService;
        private readonly IDialogService _dialogService;
        private readonly ITimelineService _timelineService;

        public ObservableCollection<MediaFile> MediaFiles { get; } = new();
        public TimelineViewModel Timeline { get; }
        public PreviewViewModel Preview { get; }

        public ICommand AddMediaCommand { get; }
        public ICommand ResetPlayheadCommand { get; }
        public ICommand ExportCommand { get; }

        public MainViewModel(
            IMediaService mediaService,
            IDialogService dialogService,
            ITimelineService timelineService,
            TimelineViewModel timelineViewModel,
            PreviewViewModel previewViewModel)
        {
            _mediaService = mediaService;
            _dialogService = dialogService;
            _timelineService = timelineService;
            Timeline = timelineViewModel;
            Preview = previewViewModel;

            AddMediaCommand = new RelayCommand(ExecuteAddMedia);
            ResetPlayheadCommand = new RelayCommand(ExecuteResetPlayhead);
            ExportCommand = new RelayCommand(ExecuteExport);
        }

        private void ExecuteResetPlayhead(object parameter)
        {
            Timeline.PlayheadPosition = 0;
            Preview.Reset();
        }

        private async void ExecuteAddMedia(object parameter)
        {
            var fileNames = _dialogService.ShowOpenFileDialog(
                "Media Files|*.mp4;*.avi;*.mkv;*.mp3;*.wav;*.jpg;*.png",
                multiselect: true);

            if (fileNames == null || fileNames.Length == 0)
                return;

            // ✅ 1. БЫСТРОЕ добавление БЕЗ thumbnails и STA проблем
            foreach (string filePath in fileNames)
            {
                try
                {
                    // ✅ Создаем MediaFile напрямую (без UI вызовов)
                    var mediaFile = new MediaFile(filePath); // Предполагаем конструктор

                    // Добавляем в UI потоке
                    MediaFiles.Add(mediaFile);
                    _timelineService.AddClipToTimeline(mediaFile, Timeline);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Ошибка: {ex.Message}", "Ошибка");
                }
            }

            Preview.UpdateTotalDuration();

            // ✅ 2. Отложенная генерация thumbnails (через 1 сек)
            _ = Task.Delay(1000).ContinueWith(_ => GenerateThumbnailsAsync());
        }

        private async void GenerateThumbnailsAsync()
        {
            foreach (MediaFile mediaFile in MediaFiles.ToList())
            {
                if (string.IsNullOrEmpty(mediaFile.ThumbnailPath))
                {
                    try
                    {
                        // ✅ Dispatcher для безопасного UI обновления
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // Простая заглушка thumbnail
                            mediaFile.ThumbnailPath = "pack://application:,,,/Resources/placeholder.png";
                        });
                    }
                    catch { }
                }
            }
        }

        private void ExecuteExport(object parameter)
        {
            var fileName = _dialogService.ShowSaveFileDialog("MP4 Files|*.mp4", "output.mp4");
            if (fileName != null)
            {
                _dialogService.ShowMessage($"Export to {fileName}\n(FFmpeg soon!)", "Export");
            }
        }
    }
}
