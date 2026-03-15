using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VideoEditorWPF.Factories;
using VideoEditorWPF.Models;
using VideoEditorWPF.Services;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;
        private ITimelineRenderService _timelineRenderService;
        private ITrackRenderService _trackRenderService;
        private IPreviewRenderService _previewRenderService;

        private Point _lastMousePos;
        private Clip _draggedClip;
        private Rectangle _draggedVisual;
        private TextBlock _draggedLabel;

        // Snap indicator visuals
        private Line _snapIndicatorLine;
        private TextBlock _snapIndicatorLabel;
        private Border _snapIndicatorBorder;

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
        }

        private void InitializeServices()
        {
            IMediaService mediaService = new MediaService();
            IDialogService dialogService = new DialogService();
            ITimelineService timelineService = new TimelineService();
            IClipFactory clipFactory = new ClipFactory();

            var timelineViewModel = new TimelineViewModel(clipFactory);
            var previewViewModel = new PreviewViewModel(timelineViewModel);
            var mainViewModel = new MainViewModel(mediaService, dialogService, timelineService, timelineViewModel, previewViewModel);

            IClipRenderService clipRenderService = new ClipRenderService();
            _trackRenderService = new TrackRenderService(clipRenderService);
            _timelineRenderService = new TimelineRenderService();

            // Инициализация PreviewRenderService
            _previewRenderService = new PreviewRenderService();
            PreviewCanvas.Source = _previewRenderService.InitializePreview();

            DataContext = mainViewModel;
            SetupEventHandlers();
            SetupPreviewIntegration();
        }

        private void SetupPreviewIntegration()
        {
            // Подписываемся на событие запроса кадра
            ViewModel.Preview.PreviewFrameNeeded += OnPreviewFrameNeeded;

            // Устанавливаем FPS
            _previewRenderService.SetPreviewFPS(ViewModel.Preview.PreviewFPS);
        }

        private void OnPreviewFrameNeeded(TimeSpan time)
        {
            // Обновляем превью для текущего времени
            if (PreviewCanvas.Source is System.Windows.Media.Imaging.WriteableBitmap bitmap)
            {
                _previewRenderService.UpdatePreview(bitmap, time, ViewModel.Timeline.Tracks);
            }
        }

        private void SetupEventHandlers()
        {
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
            // Force layout update to ensure ViewportWidth is calculated
            UpdateLayout();
            TimelineScrollViewer.UpdateLayout();

            // Wait for rendering to complete
            Dispatcher.InvokeAsync(() =>
            {
                // Initialize minimum scale after viewport is ready
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
            // Try ViewportWidth first
            if (TimelineScrollViewer.ViewportWidth > 0)
                return TimelineScrollViewer.ViewportWidth;

            // Fallback to ActualWidth (for fullscreen initial render)
            if (TimelineScrollViewer.ActualWidth > 0)
                return TimelineScrollViewer.ActualWidth;

            // Last resort: calculate from grid column
            return 0;
        }

        private void RefreshTimeline()
        {
            // Get reliable viewport width
            double viewportWidth = GetTimelineViewportWidth();

            if (viewportWidth == 0)
            {
                // Retry after layout is complete
                Dispatcher.InvokeAsync(() => RefreshTimeline(), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            // Calculate dynamic canvas width based on timeline length and scale
            // Add 50 pixels padding at the end to ensure the last label is visible
            const double endPadding = 50;
            double timelineWidth = (ViewModel.Timeline.TimelineLength * ViewModel.Timeline.TimelineScale) + endPadding;

            // Ensure canvas is at least as wide as viewport to prevent centering/offset
            double canvasWidth = Math.Max(timelineWidth, viewportWidth);

            // Update ruler canvas width
            TimeRulerCanvas.Width = canvasWidth;
            TimeRulerCanvas.Children.Clear();
            _timelineRenderService.RenderTimeline(TimeRulerCanvas, ViewModel.Timeline.TimelineScale, viewportWidth, ViewModel.Timeline.TimelineLength);

            // Add playhead to ruler after rendering
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

            // Update timeline canvas width
            TimelineCanvas.Width = canvasWidth;

            RefreshTracks();
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
                UpdateRulerPlayhead();
                UpdateTimelinePlayhead();  // Add this line
            }
        }

        private void UpdateRulerPlayhead()
        {
            var playhead = TimeRulerCanvas.Children.OfType<Rectangle>()
                .FirstOrDefault(r => r.Name == "RulerPlayhead");

            if (playhead != null)
            {
                Canvas.SetLeft(playhead, ViewModel.Timeline.PlayheadPosition);
            }
        }

        /// <summary>
        /// Updates the timeline canvas playhead position
        /// </summary>
        private void UpdateTimelinePlayhead()
        {
            if (Playhead != null)
            {
                Canvas.SetLeft(Playhead, ViewModel.Timeline.PlayheadPosition);
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

        private void MediaLibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MediaLibraryList.SelectedItem is MediaFile mediaFile)
            {
                ViewModel.Timeline.AddClipToTrack(mediaFile);
            }
        }

        private void RefreshTracks()
        {
            _trackRenderService.ClearTracks(TimelineCanvas);

            // Pass the canvas width (without padding for track backgrounds)
            double canvasWidth = ViewModel.Timeline.TimelineLength * ViewModel.Timeline.TimelineScale;
            _trackRenderService.RenderTracks(TimelineCanvas, ViewModel.Timeline.Tracks, canvasWidth, ViewModel.Timeline.TimelineScale);
            DrawTrackSeparators();
        }

        private void DrawTrackSeparators()
        {
            // Remove old separator lines
            var oldLines = TimelineCanvas.Children.OfType<Line>()
                .Where(l => l.Tag?.ToString() == "TrackSeparator")
                .ToList();
            foreach (var line in oldLines)
            {
                TimelineCanvas.Children.Remove(line);
            }

            // Calculate dynamic width based on timeline length and scale
            double separatorWidth = ViewModel.Timeline.TimelineLength * ViewModel.Timeline.TimelineScale;

            // Draw horizontal separator lines between tracks
            for (int i = 0; i < ViewModel.Timeline.Tracks.Count; i++)
            {
                double y = (i + 1) * 70; // Bottom edge of each track

                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = separatorWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(62, 62, 66)), // #FF3E3E42
                    StrokeThickness = 1,
                    Tag = "TrackSeparator"
                };

                Canvas.SetZIndex(line, -1); // Behind clips
                TimelineCanvas.Children.Add(line);
            }
        }

        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePos = e.GetPosition(TimelineCanvas);

            var hitClip = TimelineCanvas.InputHitTest(e.GetPosition(TimelineCanvas)) as DependencyObject;
            while (hitClip != null && hitClip != TimelineCanvas)
            {
                if (hitClip is Rectangle rect && rect.Tag is Clip clip)
                {
                    _draggedClip = clip;
                    _draggedVisual = rect;

                    _draggedLabel = TimelineCanvas.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => tb.Tag == clip);

                    TimelineCanvas.CaptureMouse();
                    return;
                }
                hitClip = LogicalTreeHelper.GetParent(hitClip);
            }

            // Moving playhead - apply snapping if Shift is pressed
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            double playheadPosition = SnapToGrid(_lastMousePos.X, isShiftPressed);

            Canvas.SetLeft(Playhead, playheadPosition);
            ViewModel.Timeline.PlayheadPosition = playheadPosition;
        }

        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedClip != null && TimelineCanvas.IsMouseCaptured)
            {
                var currentPos = e.GetPosition(TimelineCanvas);
                bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                // Get current visual X position from the actual rendered rectangle
                double visualStartX = _draggedVisual != null
                    ? Canvas.GetLeft(_draggedVisual)
                    : _draggedClip.GetOffsetPixels(ViewModel.Timeline.TimelineScale);

                // Calculate delta from last position
                var deltaX = currentPos.X - _lastMousePos.X;

                // Apply delta to visual position
                double newStartX = Math.Max(0, visualStartX + deltaX);

                // Snap to 0.5s grid if Shift is pressed
                if (isShiftPressed)
                {
                    newStartX = SnapToGrid(newStartX, true);
                    ShowSnapIndicator(newStartX);
                }
                else
                {
                    HideSnapIndicator();
                }

                // Update model (converts pixels to time using current scale)
                ViewModel.Timeline.UpdateClipTimePosition(_draggedClip, newStartX);

                // Update visuals
                if (_draggedVisual != null)
                {
                    Canvas.SetLeft(_draggedVisual, newStartX);
                }

                if (_draggedLabel != null)
                {
                    Canvas.SetLeft(_draggedLabel, newStartX + 5);
                }

                _lastMousePos = currentPos;
            }
        }

        private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedClip != null)
            {
                RefreshTracks();
            }

            HideSnapIndicator(); // Hide snap indicator when drag ends

            TimelineCanvas.ReleaseMouseCapture();
            _draggedClip = null;
            _draggedVisual = null;
            _draggedLabel = null;
        }

        private void TimelineScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // The TimelineScale property setter will handle min/max constraints
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
                // The TimelineScale property setter will handle min/max constraints
                ViewModel.Timeline.TimelineScale *= e.Delta > 0 ? 1.414 : 0.707;
                e.Handled = true;
            }
        }

        private void TrackHeadersScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                TimelineScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            }
        }

        private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                TrackHeadersScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            }

            if (e.HorizontalChange != 0)
            {
                TimeRulerScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
            }
        }

        private void TimeRulerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clickPosition = e.GetPosition(TimeRulerCanvas);
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // Snap to 0.5s grid if Shift is pressed
            double playheadPosition = SnapToGrid(clickPosition.X, isShiftPressed);

            ViewModel.Timeline.PlayheadPosition = playheadPosition;

            e.Handled = true;
        }

        private double SnapToGrid(double pixelPosition, bool isShiftPressed)
        {
            if (!isShiftPressed)
                return pixelPosition;

            const double snapInterval = 0.5; // 0.5 seconds
            double timelineScale = ViewModel.Timeline.TimelineScale;

            // Convert pixels to seconds
            double timeInSeconds = pixelPosition / timelineScale;

            // Round to nearest 0.5 second interval
            double snappedSeconds = Math.Round(timeInSeconds / snapInterval) * snapInterval;

            // Convert back to pixels
            return snappedSeconds * timelineScale;
        }

        private void ShowSnapIndicator(double pixelPosition)
        {
            double timelineScale = ViewModel.Timeline.TimelineScale;
            double timeInSeconds = pixelPosition / timelineScale;

            // Create or update the snap line
            if (_snapIndicatorLine == null)
            {
                _snapIndicatorLine = new Line
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 215, 0)), // Gold
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                Canvas.SetZIndex(_snapIndicatorLine, 998); // Just below playhead
                TimelineCanvas.Children.Add(_snapIndicatorLine);
            }

            _snapIndicatorLine.X1 = pixelPosition;
            _snapIndicatorLine.X2 = pixelPosition;
            _snapIndicatorLine.Y1 = 0;
            _snapIndicatorLine.Y2 = ViewModel.Timeline.CalculatedHeight;
            _snapIndicatorLine.Visibility = Visibility.Visible;

            // Create or update the snap label
            if (_snapIndicatorBorder == null)
            {
                _snapIndicatorLabel = new TextBlock
                {
                    Foreground = Brushes.Black,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(4, 2, 4, 2)
                };

                _snapIndicatorBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 215, 0)), // Gold
                    CornerRadius = new CornerRadius(3),
                    Child = _snapIndicatorLabel,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(218, 165, 32)), // GoldenRod
                    BorderThickness = new Thickness(1)
                };
                Canvas.SetZIndex(_snapIndicatorBorder, 999);
                TimelineCanvas.Children.Add(_snapIndicatorBorder);
            }

            // Format time as mm:ss.f
            var timeSpan = TimeSpan.FromSeconds(timeInSeconds);
            string timeText = timeInSeconds >= 60
                ? $"{timeSpan:mm\\:ss\\.f}"
                : $"{timeSpan:ss\\.f}s";

            _snapIndicatorLabel.Text = timeText;

            // Position label above the line
            Canvas.SetLeft(_snapIndicatorBorder, pixelPosition + 5);
            Canvas.SetTop(_snapIndicatorBorder, 5);
            _snapIndicatorBorder.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the snap indicator
        /// </summary>
        private void HideSnapIndicator()
        {
            if (_snapIndicatorLine != null)
                _snapIndicatorLine.Visibility = Visibility.Collapsed;

            if (_snapIndicatorBorder != null)
                _snapIndicatorBorder.Visibility = Visibility.Collapsed;
        }
    }
}
