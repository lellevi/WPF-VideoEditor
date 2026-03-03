using System;
using System.Collections.ObjectModel;
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

        public ICommand AddMediaCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand PreviousFrameCommand { get; }
        public ICommand NextFrameCommand { get; }
        public ICommand ExportCommand { get; }

        public MainViewModel(
            IMediaService mediaService,
            IDialogService dialogService,
            ITimelineService timelineService,
            TimelineViewModel timelineViewModel)
        {
            _mediaService = mediaService;
            _dialogService = dialogService;
            _timelineService = timelineService;
            Timeline = timelineViewModel;

            AddMediaCommand = new RelayCommand(ExecuteAddMedia);
            PlayPauseCommand = new RelayCommand(ExecutePlayPause);
            PreviousFrameCommand = new RelayCommand(ExecutePreviousFrame);
            NextFrameCommand = new RelayCommand(ExecuteNextFrame);
            ExportCommand = new RelayCommand(ExecuteExport);
        }

        private void ExecuteAddMedia(object parameter)
        {
            var fileNames = _dialogService.ShowOpenFileDialog(
                "Media Files|*.mp4;*.avi;*.mkv;*.mp3;*.wav;*.jpg;*.png",
                multiselect: true);

            if (fileNames == null || fileNames.Length == 0)
                return;

            var mediaFiles = _mediaService.LoadMultipleMediaFiles(fileNames);
            foreach (var mediaFile in mediaFiles)
            {
                MediaFiles.Add(mediaFile);
                _timelineService.AddClipToTimeline(mediaFile, Timeline);
            }
        }

        private void ExecutePlayPause(object parameter)
        {
            Timeline.IsPlaying = !Timeline.IsPlaying;
        }

        private void ExecutePreviousFrame(object parameter)
        {
            Timeline.PlayheadPosition = Math.Max(0, Timeline.PlayheadPosition - Timeline.TimelineScale);
        }

        private void ExecuteNextFrame(object parameter)
        {
            Timeline.PlayheadPosition += Timeline.TimelineScale;
        }

        private void ExecuteExport(object parameter)
        {
            var fileName = _dialogService.ShowSaveFileDialog("MP4 Files|*.mp4", "output.mp4");

            if (fileName != null)
            {
                _dialogService.ShowMessage(
                    $"Export to {fileName}\n(FFmpeg integration coming soon!)", // TODO
                    "Export");
            }
        }
    }
}
