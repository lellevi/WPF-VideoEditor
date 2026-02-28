using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VideoEditorWPF.Models;
using VideoEditorWPF.Services;

namespace VideoEditorWPF
{
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly VideoService _videoService = new();
        private readonly TimelineService _timelineService = new();

        public ObservableCollection<VideoClip> MediaFiles { get; set; } = new();
        public ObservableCollection<VideoClip> TimelineClips { get; set; } = new();

        private VideoClip? _selectedMediaFile;
        public VideoClip? SelectedMediaFile
        {
            get => _selectedMediaFile;
            set
            {
                _selectedMediaFile = value;
                OnPropertyChanged();
                UpdatePreview();
            }
        }

        private BitmapImage? _previewImage;
        public BitmapImage? PreviewImage
        {
            get => _previewImage;
            set { _previewImage = value; OnPropertyChanged(); }
        }

        private double _previewPosition;
        public double PreviewPosition
        {
            get => _previewPosition;
            set
            {
                _previewPosition = value;
                OnPropertyChanged();
                UpdatePreviewImage();
            }
        }

        private string _currentTime = "00:00";
        public string CurrentTime
        {
            get => _currentTime;
            set { _currentTime = value; OnPropertyChanged(); }
        }

        private double _playheadPosition;
        public double PlayheadPosition
        {
            get => _playheadPosition;
            set { _playheadPosition = value; OnPropertyChanged(); }
        }

        private double _timelineWidth = 2000;
        public double TimelineWidth
        {
            get => _timelineWidth;
            set { _timelineWidth = value; OnPropertyChanged(); }
        }

        private double _trimStart;
        public double TrimStart
        {
            get => _trimStart;
            set
            {
                _trimStart = Math.Max(0, Math.Min(value, TrimEnd));
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrimStartText));
                OnPropertyChanged(nameof(TrimDuration));
                OnPropertyChanged(nameof(TrimmedWidth));
            }
        }

        private double _trimEnd;
        public double TrimEnd
        {
            get => _trimEnd;
            set
            {
                _trimEnd = Math.Max(TrimStart, Math.Min(value, SelectedMediaFile?.Duration ?? 0));
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrimEndText));
                OnPropertyChanged(nameof(TrimDuration));
                OnPropertyChanged(nameof(TrimmedWidth));
            }
        }

        private double _exportProgress;
        public double ExportProgress
        {
            get => _exportProgress;
            set { _exportProgress = value; OnPropertyChanged(); }
        }

        private string _exportStatus = "";
        public string ExportStatus
        {
            get => _exportStatus;
            set { _exportStatus = value; OnPropertyChanged(); }
        }

        public ICommand ImportVideoCommand { get; }
        public ICommand AddToTimelineCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand TrimClipCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand PreviousFrameCommand { get; }
        public ICommand NextFrameCommand { get; }

        private bool _isPlaying;
        private readonly DispatcherTimer _previewTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };

        public MainViewModel()
        {
            ImportVideoCommand = new RelayCommand(async () => await ImportVideo());
            AddToTimelineCommand = new RelayCommand(AddToTimeline);
            PlayPauseCommand = new RelayCommand(PlayPause);
            TrimClipCommand = new RelayCommand(TrimClip);
            ExportCommand = new RelayCommand(async () => await ExportVideo());
            PreviousFrameCommand = new RelayCommand(PreviousFrame);
            NextFrameCommand = new RelayCommand(NextFrame);

            _previewTimer.Tick += PreviewTimer_Tick;
        }

        private void PreviewTimer_Tick(object? sender, EventArgs e)
        {
            UpdatePreviewFrame();
        }

        private async Task ImportVideo()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.avi;*.mkv)|*.mp4;*.avi;*.mkv"
            };
            if (dialog.ShowDialog() == true)
            {
                var clip = await _videoService.LoadVideoAsync(dialog.FileName);
                MediaFiles.Add(clip);
            }
        }

        private void AddToTimeline()
        {
            if (SelectedMediaFile != null)
            {
                var timelineClip = new VideoClip
                {
                    FilePath = SelectedMediaFile.FilePath,
                    Duration = SelectedMediaFile.Duration,
                    TrimStart = SelectedMediaFile.TrimStart,
                    TrimEnd = SelectedMediaFile.TrimEnd,
                    LeftPosition = TimelineClips.Sum(c => c.Width) + 50,
                    Width = (SelectedMediaFile.TrimEnd - SelectedMediaFile.TrimStart) * 20
                };
                TimelineClips.Add(timelineClip);
                TimelineWidth = Math.Max(TimelineWidth, timelineClip.LeftPosition + timelineClip.Width + 100);
            }
        }

        private void PlayPause()
        {
            _isPlaying = !_isPlaying;
            if (_isPlaying)
                _previewTimer.Start();
            else
                _previewTimer.Stop();
        }

        private void UpdatePreviewFrame()
        {
            if (SelectedMediaFile == null) return;

            if (_isPlaying)
            {
                PreviewPosition += 1.5;
                if (PreviewPosition > 100) PreviewPosition = 0;
            }
        }

        private void UpdatePreview()
        {
            if (SelectedMediaFile?.FilePath != null)
            {
                TrimStart = 0;
                TrimEnd = SelectedMediaFile.Duration;
                PreviewPosition = 0;
                UpdatePreviewImage();
                OnPropertyChanged(nameof(VideoDuration));
            }
        }

        public void TrimClip()
        {
            if (SelectedMediaFile != null)
            {
                SelectedMediaFile.TrimStart = TrimStart;
                SelectedMediaFile.TrimEnd = TrimEnd;
                SelectedMediaFile.OnPropertyChanged(string.Empty);
                PreviewPosition = (TrimStart / SelectedMediaFile.Duration) * 100;
                UpdatePreviewImage();
            }
        }

        private async Task ExportVideo()
        {
            if (!TimelineClips.Any())
            {
                ExportStatus = "Добавьте клипы на timeline!";
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MP4 files (*.mp4)|*.mp4|All files (*.*)|*.*",
                DefaultExt = "mp4",
                FileName = $"VideoEditor_{DateTime.Now:yyyyMMdd_HHmmss}.mp4"
            };

            if (dialog.ShowDialog() == true)
            {
                ExportStatus = "Экспорт...";
                ExportProgress = 0;

                await _timelineService.ExportTimelineAsync(TimelineClips.ToList(), dialog.FileName, progress =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ExportProgress = progress;
                    });
                });

                ExportStatus = $"Сохранено: {Path.GetFileName(dialog.FileName)}";
                ExportProgress = 100;

                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
            }
        }

        private void PreviousFrame()
        {
            PreviewPosition = Math.Max(0, PreviewPosition - 3);
        }

        private void NextFrame()
        {
            PreviewPosition = Math.Min(100, PreviewPosition + 3);
        }

        private void UpdatePreviewImage()
        {
            if (SelectedMediaFile == null) return;

            var timeSeconds = PreviewPosition / 100.0 * SelectedMediaFile.Duration;
            PreviewImage = _videoService.GetPreviewFrame(SelectedMediaFile, timeSeconds);
            CurrentTime = TimeSpan.FromSeconds(timeSeconds).ToString(@"mm\:ss");
        }

        public string VideoDuration => SelectedMediaFile != null
            ? TimeSpan.FromSeconds(SelectedMediaFile.Duration).ToString(@"mm\:ss")
            : "00:00";

        public string TrimStartText => TimeSpan.FromSeconds(TrimStart).ToString(@"mm\:ss");
        public string TrimEndText => TimeSpan.FromSeconds(TrimEnd).ToString(@"mm\:ss");
        public string TrimDuration => TimeSpan.FromSeconds(Math.Max(0, TrimEnd - TrimStart)).ToString(@"mm\:ss");

        public double TrimmedWidth => SelectedMediaFile != null && SelectedMediaFile.Duration > 0
            ? Math.Max(20, (TrimEnd - TrimStart) / SelectedMediaFile.Duration * 280)
            : 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
