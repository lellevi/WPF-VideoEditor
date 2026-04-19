using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VideoEditorWPF.Factories;
using VideoEditorWPF.Models;
using VideoEditorWPF.Services;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; }

        private ITimelineRenderService _timelineRenderService;
        private ITrackRenderService _trackRenderService;
        private IPreviewRenderService _previewRenderService;
        private ITimelineInteractionService _interactionService;
        private IPlayheadService _playheadService;
        private IScrollSyncService _scrollSyncService;

        private ClipDragInfo _dragInfo;

        private readonly IMediaService _mediaService = new MediaService();
        private readonly IDialogService _dialogService = new DialogService();
        private readonly ITimelineService _timelineService = new TimelineService();

        public MainWindow()
        {
            InitializeComponent();
            InitializeEverything();
        }

        private void InitializeEverything()
        {
            var clipFactory = new ClipFactory();
            var timelineVM = new TimelineViewModel(clipFactory);

            timelineVM.TracksChanged += OnTimelineTracksChanged;

            _previewRenderService = new PreviewRenderService();
            var previewVM = new PreviewViewModel(timelineVM, _previewRenderService);

            ViewModel = new MainViewModel(_mediaService, _dialogService, _timelineService, timelineVM, previewVM);

            DataContext = ViewModel;

            var clipRenderService = new ClipRenderService();
            _trackRenderService = new TrackRenderService(clipRenderService);
            _timelineRenderService = new TimelineRenderService();

            ISnapIndicatorService snapIndicator = new SnapIndicatorService();
            _interactionService = new TimelineInteractionService(snapIndicator);
            _playheadService = new PlayheadService();
            _scrollSyncService = new ScrollSyncService();

            PreviewCanvas.Source = _previewRenderService.InitializePreview();

            SetupEventHandlers();
            SetupPreviewIntegration();
        }

        // В MainWindow.xaml.cs - добавьте метод RefreshTracks с форсированным обновлением

        public void RefreshTracks()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Очищаем и перерисовываем все треки
                    double canvasWidth = TimelineCanvas.Width;
                    if (canvasWidth <= 0)
                        canvasWidth = 1000;

                    _trackRenderService.ClearTracks(TimelineCanvas);
                    _trackRenderService.RenderTracks(TimelineCanvas, ViewModel.Timeline.Tracks, canvasWidth, ViewModel.Timeline.TimelineScale);
                    DrawTrackSeparators();

                    // Обновляем высоту Canvas
                    TimelineCanvas.Height = ViewModel.Timeline.CalculatedHeight;

                    // Обновляем плейхед
                    if (Playhead != null)
                    {
                        Playhead.Height = ViewModel.Timeline.CalculatedHeight;
                        Canvas.SetLeft(Playhead, ViewModel.Timeline.PlayheadPosition);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RefreshTracks error: {ex.Message}");
                }
            });
        }

        private void OnTimelineTracksChanged()
        {
            Dispatcher.Invoke(() => RefreshTracks());
        }

        private void TrackRenderService_ClipPropertyChanged(object sender, ClipPropertyChangedEventArgs e)
        {
            RefreshTracks();
        }

        private void SetupPreviewIntegration()
        {
            ViewModel.Preview.PreviewFrameNeeded += OnPreviewFrameNeeded;
            ViewModel.Preview.PlayheadPositionChanged += OnPlayheadPositionChanged;
            _previewRenderService.SetPreviewFPS(ViewModel.Preview.PreviewFPS);
        }

        private void OnPlayheadPositionChanged(double seconds)
        {
            Dispatcher.Invoke(() =>
            {
                double pixelPosition = seconds * ViewModel.Timeline.TimelineScale;
                ViewModel.Timeline.PlayheadPosition = pixelPosition;

                if (TimelineSlider != null)
                {
                    TimelineSlider.Value = seconds;
                }
            });
        }

        private async void OnPreviewFrameNeeded(TimeSpan time)
        {
            if (PreviewCanvas.Source is System.Windows.Media.Imaging.WriteableBitmap bitmap)
            {
                try
                {
                    await _previewRenderService.UpdatePreview(bitmap, time, ViewModel.Timeline.Tracks);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Ошибка обновления кадра: {ex.Message}");
                }
            }
            else
            {
                throw new ArgumentException("PreviewCanvas.Source не является WriteableBitmap!");
            }
        }

        private async void MediaLibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await MediaLibraryList_MouseDoubleClickAsync(sender, e);
        }

        private async Task MediaLibraryList_MouseDoubleClickAsync(object sender, MouseButtonEventArgs e)
        {
            if (MediaLibraryList.SelectedItem is MediaFile mediaFile)
            {
                var mediaFileReal = await _mediaService.LoadMediaFileAsync(mediaFile.FilePath);

                ViewModel.Timeline.AddClipToTrack(mediaFileReal);
                _previewRenderService.PreloadVideoFrames(mediaFileReal.FilePath);
            }
        }

        private void SetupEventHandlers()
        {
            MediaLibraryList.MouseDoubleClick += MediaLibraryList_MouseDoubleClick;
            TimelineScrollViewer.MouseWheel += TimelineScrollViewer_MouseWheel;
            TimelineScrollViewer.SizeChanged += TimelineScrollViewer_SizeChanged;

            ViewModel.Timeline.Tracks.CollectionChanged += Tracks_CollectionChanged;

            foreach (var track in ViewModel.Timeline.Tracks)
            {
                track.Clips.CollectionChanged += Clips_CollectionChanged;
            }

            ViewModel.Timeline.TimelineScaleChanged += TimelineScaleChanged_Handler;
            ViewModel.Timeline.PropertyChanged += Timeline_PropertyChanged;
            Loaded += Window_Loaded;
            Closing += Window_Closing;
        }

        private void TimelineScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged && ViewModel?.Timeline != null)
            {
                double viewportWidth = GetTimelineViewportWidth();
                if (viewportWidth > 0)
                {
                    ViewModel.Timeline.SetMinimumScale(viewportWidth);
                    RefreshTimeline();
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLayout();
            TimelineScrollViewer.UpdateLayout();

            Dispatcher.InvokeAsync(() =>
            {
                if (ViewModel?.Timeline != null)
                {
                    double viewportWidth = GetTimelineViewportWidth();
                    if (viewportWidth > 0)
                    {
                        ViewModel.Timeline.SetMinimumScale(viewportWidth);
                    }
                }

                RefreshTimeline();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private double GetTimelineViewportWidth()
        {
            if (TimelineScrollViewer.ViewportWidth > 0)
                return TimelineScrollViewer.ViewportWidth;

            if (TimelineScrollViewer.ActualWidth > 0)
                return TimelineScrollViewer.ActualWidth;

            return 0;
        }

        private void RefreshTimeline()
        {
            double viewportWidth = GetTimelineViewportWidth();

            if (viewportWidth == 0)
            {
                Dispatcher.InvokeAsync(() => RefreshTimeline(), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            const double endPadding = 50;
            double baseTimelineWidth = ViewModel.Timeline.TimelineLength * ViewModel.Timeline.TimelineScale;
            double timelineWidth = baseTimelineWidth + endPadding;
            double canvasWidth = Math.Max(timelineWidth, viewportWidth);

            TimeRulerCanvas.Width = canvasWidth;
            TimeRulerCanvas.Children.Clear();
            _timelineRenderService.RenderTimeline(TimeRulerCanvas, ViewModel.Timeline.TimelineScale, viewportWidth, ViewModel.Timeline.TimelineLength);

            var rulerPlayhead = new Rectangle
            {
                Width = 3,
                Height = 35,
                Fill = new SolidColorBrush(Colors.Red),
                Name = "RulerPlayhead"
            };
            Canvas.SetLeft(rulerPlayhead, ViewModel.Timeline.PlayheadPosition);
            Canvas.SetZIndex(rulerPlayhead, 1000);
            TimeRulerCanvas.Children.Add(rulerPlayhead);

            TimelineCanvas.Width = canvasWidth;

            RefreshTracks();
        }

        private void DrawTrackSeparators()
        {
            var oldLines = TimelineCanvas.Children.OfType<Line>()
                .Where(l => l.Tag?.ToString() == "TrackSeparator")
                .ToList();
            foreach (var line in oldLines)
            {
                TimelineCanvas.Children.Remove(line);
            }

            double separatorWidth = TimelineCanvas.Width;

            for (int i = 0; i < ViewModel.Timeline.Tracks.Count; i++)
            {
                double y = (i + 1) * 70;

                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = separatorWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                    StrokeThickness = 1,
                    Tag = "TrackSeparator"
                };

                Canvas.SetZIndex(line, -1);
                TimelineCanvas.Children.Add(line);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TimelineScrollViewer.MouseWheel -= TimelineScrollViewer_MouseWheel;
            TimelineScrollViewer.SizeChanged -= TimelineScrollViewer_SizeChanged;

            ViewModel.Timeline.Tracks.CollectionChanged -= Tracks_CollectionChanged;

            foreach (var track in ViewModel.Timeline.Tracks)
            {
                track.Clips.CollectionChanged -= Clips_CollectionChanged;
            }

            ViewModel.Timeline.TimelineScaleChanged -= TimelineScaleChanged_Handler;
            ViewModel.Timeline.PropertyChanged -= Timeline_PropertyChanged;
            Loaded -= Window_Loaded;
            Closing -= Window_Closing;
        }

        private void Timeline_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Timeline.PlayheadPosition))
            {
                _playheadService.SyncPlayheads(TimeRulerCanvas, Playhead, ViewModel.Timeline.PlayheadPosition);
            }
        }

        private void TimelineScaleChanged_Handler(object sender, System.EventArgs e)
        {
            RefreshTimeline();
        }

        private void Tracks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Track track in e.NewItems)
                {
                    track.Clips.CollectionChanged += Clips_CollectionChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (Track track in e.OldItems)
                {
                    track.Clips.CollectionChanged -= Clips_CollectionChanged;
                }
            }
            RefreshTracks();
        }

        private void Clips_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshTracks();
        }

        private void TrackHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Track track)
            {
                ViewModel.Timeline.SelectedTrack = track;
                e.Handled = true;
            }
        }

        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(TimelineCanvas);
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // ✅ Передаём ViewModel.Timeline
            _dragInfo = _interactionService.StartDrag(position, TimelineCanvas, ViewModel.Timeline);

            if (_dragInfo != null)
            {
                TimelineCanvas.CaptureMouse();
            }
            else
            {
                // Move playhead
                _interactionService.MovePlayhead(position, ViewModel.Timeline, Playhead, isShiftPressed);
            }
        }
        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _dragInfo != null && TimelineCanvas.IsMouseCaptured)
            {
                var currentPos = e.GetPosition(TimelineCanvas);
                bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                _interactionService.UpdateDrag(currentPos, _dragInfo, TimelineCanvas, ViewModel.Timeline, isShiftPressed);
            }
        }

        private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragInfo != null)
            {
                _interactionService.FinishDrag(_dragInfo);
                RefreshTracks();
            }

            TimelineCanvas.ReleaseMouseCapture();
            _dragInfo = null;
        }

        private void TimelineScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                ViewModel.Timeline.TimelineScale *= e.Delta > 0 ? 1.414 : 0.707;
                e.Handled = true;
            }
            else
            {
                TimelineScrollViewer.ScrollToVerticalOffset(TimelineScrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private void TimelineCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                ViewModel.Timeline.TimelineScale *= e.Delta > 0 ? 1.414 : 0.707;
                e.Handled = true;
            }
        }

        private void TrackHeadersScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                _scrollSyncService.SyncVerticalScroll(e.VerticalOffset, TimelineScrollViewer);
            }
        }

        private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                _scrollSyncService.SyncVerticalScroll(e.VerticalOffset, TrackHeadersScrollViewer);
            }

            if (e.HorizontalChange != 0)
            {
                _scrollSyncService.SyncHorizontalScroll(e.HorizontalOffset, TimeRulerScrollViewer);
            }
        }

        private void TimeRulerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clickPosition = e.GetPosition(TimeRulerCanvas);
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            double playheadPosition = _interactionService.SnapToGrid(clickPosition.X, isShiftPressed, ViewModel.Timeline.TimelineScale);
            ViewModel.Timeline.PlayheadPosition = playheadPosition;

            e.Handled = true;
        }

        private void TrackHeader_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Track track)
            {
                // Устанавливаем активную дорожку (по ПКМ)
                ViewModel.Timeline.ActiveTrack = track;

                // Опционально: показываем уведомление в статус-баре
                Debug.WriteLine($"Active track set to: {track.Name}");

                e.Handled = true;
            }
        }
    }
}
// Главное окно видеоредактора WPF. Code-behind с обработкой drag&drop клипов.
// Синхронизирует Canvas: TimelineCanvas(треки), TimeRulerCanvas(линейка), PreviewCanvas.
// Mouse события: drag клипов, Ctrl+Wheel=zoom, обычный Wheel=scroll.
// Двойной клик по MediaLibraryList → добавляет клип. События MVVM + рендер сервисы.
