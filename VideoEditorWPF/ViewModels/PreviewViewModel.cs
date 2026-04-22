using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VideoEditorWPF.Commands;
using VideoEditorWPF.Interfaces;

namespace VideoEditorWPF.ViewModels
{
    public partial class PreviewViewModel : ViewModelBase
    {
        private const int DefaultFps = 15;
        private MediaElement _mediaElement;
        private readonly TimelineViewModel _timeline;
        private readonly DispatcherTimer _renderTimer;
        private bool _isPlaying;
        private TimeSpan _currentTime;
        private TimeSpan _totalDuration;
        private int _previewFPS;
        private DateTime _lastUpdateTime;
        private bool _isUpdatingFromTimeline = false;
        private readonly IPreviewRenderService _previewRenderService;
        public event Action<TimeSpan> PreviewFrameNeeded;
        public event Action<double> PlayheadPositionChanged;
        public ICommand PlayPauseCommand { get; }
        public ICommand NextFrameCommand { get; }
        public ICommand PreviousFrameCommand { get; }
        public ICommand SeekCommand { get; }
        public PreviewViewModel(TimelineViewModel timeline, IPreviewRenderService previewService = null)
        {
            _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            _previewRenderService = previewService;
            _previewFPS = DefaultFps;
            _currentTime = TimeSpan.Zero;
            _totalDuration = TimeSpan.Zero;
            UpdateTotalDuration();
            _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / _previewFPS)
            };
            _renderTimer.Tick += OnRenderTick;
            PlayPauseCommand = new RelayCommand(ExecutePlayPause);
            NextFrameCommand = new RelayCommand(ExecuteNextFrame);
            PreviousFrameCommand = new RelayCommand(ExecutePreviousFrame);
            SeekCommand = new RelayCommand(ExecuteSeek, CanExecuteSeek);
            SubscribeToTimelineChanges();
            UpdateTotalDuration();
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged();

                    _timeline.IsPlaying = _isPlaying;

                    if (_isPlaying)
                        StartPlayback();
                    else
                        StopPlayback();
                }
            }
        }
        public TimeSpan CurrentTime
        {
            get => _currentTime;
            set
            {
                if (_isUpdatingFromTimer || _isUpdatingFromTimeline || _currentTime == value)
                    return;

                _currentTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTimeSeconds));
                if (_timeline != null)
                {
                    _timeline.PlayheadPosition = TimeSpanToPixels(value);
                }

                RequestFrame(value);
            }
        }
        public double CurrentTimeSeconds
        {
            get => _currentTime.TotalSeconds;
            set => CurrentTime = TimeSpan.FromSeconds(value);
        }

        public int PreviewFPS
        {
            get => _previewFPS;
            set
            {
                if (_previewFPS != value && value > 0 && value <= 30)
                {
                    _previewFPS = value;
                    OnPropertyChanged();
                    _previewRenderService.SetPreviewFPS(_previewFPS);
                    _renderTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _previewFPS);
                }
            }
        }

        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            set
            {
                if (_totalDuration != value)
                {
                    _totalDuration = value;
                    OnPropertyChanged();
                }
            }
        }
        private void SubscribeToTimelineChanges()
        {
            _timeline.Tracks.CollectionChanged += (s, e) =>
            {
                UpdateTotalDuration();
            };
            _timeline.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TimelineViewModel.PlayheadPosition))
                {
                    _isUpdatingFromTimeline = true;
                    try
                    {
                        var newTime = PixelsToTimeSpan(_timeline.PlayheadPosition);
                        if (Math.Abs((newTime - _currentTime).TotalSeconds) > 0.01)
                        {
                            CurrentTime = newTime;
                        }
                    }
                    finally
                    {
                        _isUpdatingFromTimeline = false;
                    }
                }
                else if (args.PropertyName == "TotalDurationSeconds")
                {
                    UpdateTotalDuration();
                }
                if (args.PropertyName == nameof(TimelineViewModel.TotalDurationSeconds))
                    UpdateTotalDuration();
            };
        }

        private bool _isUpdatingFromTimer = false;
        private void StartPlayback()
        {
            _lastUpdateTime = DateTime.Now;
            _renderTimer.Start();
        }
        private void StopPlayback()
        {
            _renderTimer.Stop();
        }
        private void RequestFrame(TimeSpan time)
        {
            PreviewFrameNeeded?.Invoke(time);
        }
        private double TimeSpanToPixels(TimeSpan time)
        {
            if (_timeline == null)
                return 0;

            return time.TotalSeconds * _timeline.TimelineScale;
        }
        private TimeSpan PixelsToTimeSpan(double pixels)
        {
            if (_timeline == null || _timeline.TimelineScale <= 0)
                return TimeSpan.Zero;

            var seconds = pixels / _timeline.TimelineScale;
            return TimeSpan.FromSeconds(seconds);
        }
        private void ExecutePlayPause(object parameter)
        {
            IsPlaying = !IsPlaying;
        }
        private void ExecuteNextFrame(object parameter)
        {
            var frameDuration = TimeSpan.FromSeconds(1.0 / PreviewFPS);
            var newTime = CurrentTime + frameDuration;
            CurrentTime = newTime > TotalDuration ? TotalDuration : newTime;
        }
        private void ExecutePreviousFrame(object parameter)
        {
            var frameDuration = TimeSpan.FromSeconds(1.0 / PreviewFPS);
            var newTime = CurrentTime - frameDuration;
            CurrentTime = newTime < TimeSpan.Zero ? TimeSpan.Zero : newTime;
        }
        private void ExecuteSeek(object parameter)
        {
            if (parameter is TimeSpan seekTime)
            {
                if (seekTime < TimeSpan.Zero)
                    seekTime = TimeSpan.Zero;
                else if (seekTime > TotalDuration)
                    seekTime = TotalDuration;

                CurrentTime = seekTime;
            }
            else if (parameter is double seconds)
            {
                ExecuteSeek(TimeSpan.FromSeconds(seconds));
            }
        }
        private bool CanExecuteSeek(object parameter)
        {
            return true;
        }
        public void Reset()
        {
            IsPlaying = false;
            CurrentTime = TimeSpan.Zero;
        }
        public void UpdateTotalDuration()
        {
            TotalDuration = _timeline.GetTotalDuration();
        }
        private void OnRenderTick(object sender, EventArgs e)
        {
            if (_isUpdatingFromTimer || !_isPlaying || TotalDuration == TimeSpan.Zero)
                return;
            _isUpdatingFromTimer = true;
            try
            {
                var frameDuration = 1.0 / PreviewFPS;
                var newTime = _currentTime + TimeSpan.FromSeconds(frameDuration);
                if (newTime >= TotalDuration)
                {
                    IsPlaying = false;
                    CurrentTime = TotalDuration;
                }
                else
                {
                    _currentTime = newTime;
                    OnPropertyChanged(nameof(CurrentTime));
                    OnPropertyChanged(nameof(CurrentTimeSeconds));
                    PlayheadPositionChanged?.Invoke(_currentTime.TotalSeconds);
                    RequestFrame(_currentTime);
                }
            }
            finally
            {
                _isUpdatingFromTimer = false;
            }
        }

        private WriteableBitmap _previewBitmap;

        public WriteableBitmap PreviewBitmap
        {
            get => _previewBitmap;
            set
            {
                _previewBitmap = value;
                OnPropertyChanged();
            }
        }
    }
}
