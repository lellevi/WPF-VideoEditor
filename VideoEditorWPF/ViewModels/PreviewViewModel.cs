using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VideoEditorWPF.Commands;

namespace VideoEditorWPF.ViewModels
{
    public partial class PreviewViewModel : ViewModelBase
    {
        private const int DEFAULT_FPS = 24;
        private MediaElement _mediaElement;
        private readonly TimelineViewModel _timeline;
        private readonly DispatcherTimer _renderTimer;
        private bool _isPlaying;
        private TimeSpan _currentTime;
        private TimeSpan _totalDuration;
        private int _previewFPS;
        private DateTime _lastUpdateTime;
        private bool _isUpdatingFromTimeline = false; // ✅ Защита от цикла

        public event Action<TimeSpan> PreviewFrameNeeded;
        public TimeSpan CurrentTime
        {
            get => _currentTime;
            set
            {
                if (_isUpdatingFromTimer || _isUpdatingFromTimeline || _currentTime == value)
                    return; // ✅ Блокируем рекурсию!

                _currentTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTimeSeconds));
                RequestFrame(value);
            }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged(); // ✅ Обновляем UI кнопки

                    // ✅ СИНХРОНИЗИРУЕМ с Timeline ПЕРЕД изменением состояния!
                    _timeline.IsPlaying = _isPlaying;

                    if (_isPlaying)
                    {
                        StartPlayback(); // ✅ Запускаем таймер
                        Console.WriteLine("▶️ PLAY STARTED"); // DEBUG
                    }
                    else
                    {
                        StopPlayback(); // ✅ Останавливаем таймер
                        Console.WriteLine("⏸ PAUSE"); // DEBUG
                    }
                }
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
                if (_previewFPS != value && value > 0 && value <= 120)
                {
                    _previewFPS = value;
                    OnPropertyChanged();
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

        public ICommand PlayPauseCommand { get; }
        public ICommand NextFrameCommand { get; }
        public ICommand PreviousFrameCommand { get; }

        public PreviewViewModel(TimelineViewModel timeline) // ✅ Убираем MediaElement
        {
            _timeline = timeline;
            _previewFPS = DEFAULT_FPS;
            _currentTime = TimeSpan.Zero;
            //_totalDuration = TimeSpan.Zero;
            _totalDuration = TimeSpan.FromMinutes(2); // ✅ ТЕСТОВЫЕ 2 МИНУТЫ!

            _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                //Interval = TimeSpan.FromMilliseconds(1000.0 / _previewFPS)
                Interval = TimeSpan.FromMilliseconds(40) // ✅ 25fps
            };
            _renderTimer.Tick += OnRenderTick;

            PlayPauseCommand = new RelayCommand(ExecutePlayPause);
            NextFrameCommand = new RelayCommand(ExecuteNextFrame);
            PreviousFrameCommand = new RelayCommand(ExecutePreviousFrame);

            SubscribeToTimelineChanges();
            UpdateTotalDuration();
        }

        // ✅ Убираем события MediaElement - используем таймер



        // ✅ ЕДИНСТВЕННЫЙ метод подписки
        private void SubscribeToTimelineChanges()
        {
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
                else if (args.PropertyName == "TotalDurationSeconds") // Без nameof для безопасности
                {
                    UpdateTotalDuration();
                }
            };
        }

        private bool _isUpdatingFromTimer = false; // ✅ Защита от рекурсии

        private void OnRenderTick(object sender, EventArgs e)
        {
            if (_isUpdatingFromTimer || !_isPlaying || TotalDuration == TimeSpan.Zero)
                return; // ✅ МГНОВЕННЫЙ выход!

            try
            {
                _isUpdatingFromTimer = true; // ✅ БЛОКИРУЕМ setter

                Console.WriteLine($"TICK! Current={_currentTime.TotalSeconds:F2}s / {TotalDuration.TotalSeconds:F2}s");

                var now = DateTime.Now;
                var elapsed = Math.Max(0.016, (now - _lastUpdateTime).TotalSeconds); // ✅ Минимум 60fps
                _lastUpdateTime = now;

                var newTime = _currentTime + TimeSpan.FromSeconds(elapsed);

                if (newTime >= TotalDuration)
                {
                    Console.WriteLine("🎬 END REACHED");
                    _isPlaying = false; // ✅ ПРЯМО тут!
                    OnPropertyChanged(nameof(IsPlaying)); // ✅ Только уведомление
                    CurrentTime = TotalDuration;
                }
                else
                {
                    // ✅ ПРЯМОЕ присвоение БЕЗ setter!
                    _currentTime = newTime;
                    OnPropertyChanged(nameof(CurrentTime));
                    OnPropertyChanged(nameof(CurrentTimeSeconds));
                    RequestFrame(_currentTime); // ✅ Один вызов!
                }
            }
            finally
            {
                _isUpdatingFromTimer = false;
            }
        }


        public void UpdateTotalDuration()
        {
            TotalDuration = _timeline.GetTotalDuration();
            Console.WriteLine($"🔧 TotalDuration UPDATED: {TotalDuration.TotalSeconds:F2} сек"); // 🔍 DEBUG
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

        private double TimeSpanToPixels(TimeSpan time) => time.TotalSeconds * _timeline.TimelineScale;
        private TimeSpan PixelsToTimeSpan(double pixels) => TimeSpan.FromSeconds(pixels / _timeline.TimelineScale);

        private void ExecutePlayPause(object parameter) => IsPlaying = !IsPlaying;

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

        public void Reset()
        {
            IsPlaying = false;
            CurrentTime = TimeSpan.Zero;
        }
    }
}
