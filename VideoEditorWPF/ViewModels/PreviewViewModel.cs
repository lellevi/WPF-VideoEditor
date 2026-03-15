using System;
using System.Windows.Input;
using System.Windows.Threading;
using VideoEditorWPF.Commands;

namespace VideoEditorWPF.ViewModels
{
    /// <summary>
    /// ViewModel для управления превью видео
    /// Обрабатывает воспроизведение, паузу, навигацию по кадрам
    /// </summary>
    public class PreviewViewModel : ViewModelBase
    {
        private const int DEFAULT_FPS = 24;

        private readonly TimelineViewModel _timeline;
        private readonly DispatcherTimer _renderTimer;

        private bool _isPlaying;
        private TimeSpan _currentTime;
        private TimeSpan _totalDuration;
        private int _previewFPS;
        private DateTime _lastUpdateTime;

        public event Action<TimeSpan> PreviewFrameNeeded;

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged();

                    // Синхронизируем с Timeline
                    if (_timeline != null)
                        _timeline.IsPlaying = value;

                    // Запускаем/останавливаем таймер
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
                if (_currentTime != value)
                {
                    _currentTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentTimeSeconds));

                    // Синхронизируем с Timeline (конвертируем TimeSpan в пиксели)
                    if (_timeline != null)
                    {
                        _timeline.PlayheadPosition = TimeSpanToPixels(value);
                    }

                    // Запрашиваем новый кадр
                    RequestFrame(value);
                }
            }
        }

        public double CurrentTimeSeconds
        {
            get => _currentTime.TotalSeconds;
            set
            {
                var newTime = TimeSpan.FromSeconds(value);
                if (_currentTime != newTime)
                {
                    CurrentTime = newTime;
                }
            }
        }

        public int PreviewFPS
        {
            get => _previewFPS;
            set
            {
                if (_previewFPS != value && value > 0 && value <= 120)
                {
                    _previewFPS = value;
                    OnPropertyChanged();

                    // Обновляем интервал таймера
                    if (_renderTimer != null)
                    {
                        _renderTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _previewFPS);
                    }
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

        public ICommand PlayPauseCommand { get; }
        public ICommand NextFrameCommand { get; }
        public ICommand PreviousFrameCommand { get; }
        public ICommand SeekCommand { get; }

        public PreviewViewModel(TimelineViewModel timeline)
        {
            _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            _previewFPS = DEFAULT_FPS;
            _currentTime = TimeSpan.Zero;
            _totalDuration = TimeSpan.FromMinutes(5);

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
        }

        private void SubscribeToTimelineChanges()
        {
            if (_timeline == null)
                return;

            // Когда пользователь двигает playhead в Timeline - обновляем CurrentTime
            _timeline.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TimelineViewModel.PlayheadPosition))
                {
                    var newTime = PixelsToTimeSpan(_timeline.PlayheadPosition);

                    // Обновляем только если значение реально изменилось
                    // (избегаем циклической синхронизации)
                    if (Math.Abs((newTime - _currentTime).TotalMilliseconds) > 10)
                    {
                        _currentTime = newTime;
                        OnPropertyChanged(nameof(CurrentTime));
                        RequestFrame(newTime);
                    }
                }
            };
        }

        private void OnRenderTick(object sender, EventArgs e)
        {
            if (!_isPlaying)
                return;

            // Вычисляем реальное прошедшее время
            var now = DateTime.Now;
            var elapsed = (now - _lastUpdateTime).TotalSeconds;
            _lastUpdateTime = now;

            // Увеличиваем текущее время
            var newTime = CurrentTime + TimeSpan.FromSeconds(elapsed);

            // Если достигли конца - останавливаем
            if (newTime >= TotalDuration)
            {
                newTime = TotalDuration;
                IsPlaying = false;
            }

            CurrentTime = newTime;
        }

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
            // Один кадр = 1/FPS секунды
            var frameDuration = TimeSpan.FromSeconds(1.0 / _previewFPS);
            var newTime = CurrentTime + frameDuration;

            if (newTime > TotalDuration)
                newTime = TotalDuration;

            CurrentTime = newTime;
        }

        private void ExecutePreviousFrame(object parameter)
        {
            // Один кадр = 1/FPS секунды
            var frameDuration = TimeSpan.FromSeconds(1.0 / _previewFPS);
            var newTime = CurrentTime - frameDuration;

            if (newTime < TimeSpan.Zero)
                newTime = TimeSpan.Zero;

            CurrentTime = newTime;
        }

        private void ExecuteSeek(object parameter)
        {
            if (parameter is TimeSpan seekTime)
            {
                // Ограничиваем диапазоном [0, TotalDuration]
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
            if (_timeline?.Tracks == null)
                return;

            double maxDuration = 0;

            // Находим максимальное время окончания всех клипов
            foreach (var track in _timeline.Tracks)
            {
                foreach (var clip in track.Clips)
                {
                    var clipEnd = clip.StartTimeSeconds + clip.DurationSeconds;
                    if (clipEnd > maxDuration)
                        maxDuration = clipEnd;
                }
            }

            TotalDuration = TimeSpan.FromSeconds(maxDuration);
        }
    }
}
