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
        public MainViewModel ViewModel { get; set; }

        private ITimelineRenderService _timelineRenderService;
        private ITrackRenderService _trackRenderService;
        private IPreviewRenderService _previewRenderService;

        private Point _lastMousePos;
        private Clip _draggedClip;
        private Rectangle _draggedVisual;
        private TextBlock _draggedLabel;

        // ✅ Определяем сервисы как поля класса
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
            // ✅ 1. Создаем ViewModels (PreviewViewModel БЕЗ MediaElement)
            var clipFactory = new ClipFactory();
            var timelineVM = new TimelineViewModel(clipFactory);
            var previewVM = new PreviewViewModel(timelineVM); // ✅ Только 1 аргумент!

            ViewModel = new MainViewModel(_mediaService, _dialogService, _timelineService, timelineVM, previewVM);
            DataContext = ViewModel;

            // ✅ 2. Инициализируем Render сервисы
            var clipRenderService = new ClipRenderService();
            _trackRenderService = new TrackRenderService(clipRenderService);
            _timelineRenderService = new TimelineRenderService();
            _previewRenderService = new PreviewRenderService();

            PreviewCanvas.Source = _previewRenderService.InitializePreview();

            // ✅ 3. Настраиваем события
            SetupEventHandlers();
            SetupPreviewIntegration();
        }

        private void SetupPreviewIntegration()
        {
            ViewModel.Preview.PreviewFrameNeeded += OnPreviewFrameNeeded;
        }

        private void OnPreviewFrameNeeded(TimeSpan time)
        {
            if (PreviewCanvas.Source is System.Windows.Media.Imaging.WriteableBitmap bitmap)
            {
                _previewRenderService.UpdatePreview(bitmap, time, ViewModel.Timeline.Tracks);
            }
        }

        private void SetupEventHandlers()
        {
            TimelineScrollViewer.MouseWheel += TimelineScrollViewer_MouseWheel;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshTimeline();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Отписываемся от событий
            TimelineScrollViewer.MouseWheel -= TimelineScrollViewer_MouseWheel;
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

        // ✅ Остальные методы БЕЗ ИЗМЕНЕНИЙ (копируйте из вашего кода):
        private void Timeline_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Timeline.PlayheadPosition))
            {
                UpdateRulerPlayhead();
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

        private void RefreshTimeline()
        {
            TimeRulerCanvas.Children.Clear();
            _timelineRenderService.RenderTimeline(TimeRulerCanvas, ViewModel.Timeline.TimelineScale, TimelineScrollViewer.ViewportWidth);

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

            RefreshTracks();
        }

        private void RefreshTracks()
        {
            _trackRenderService.ClearTracks(TimelineCanvas);
            _trackRenderService.RenderTracks(TimelineCanvas, ViewModel.Timeline.Tracks, TimelineScrollViewer.ViewportWidth);
            DrawTrackSeparators();
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

            for (int i = 0; i < ViewModel.Timeline.Tracks.Count; i++)
            {
                double y = (i + 1) * 70;
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = 4000,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                    StrokeThickness = 1,
                    Tag = "TrackSeparator"
                };
                Canvas.SetZIndex(line, -1);
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

            Canvas.SetLeft(Playhead, _lastMousePos.X);
            ViewModel.Timeline.PlayheadPosition = _lastMousePos.X;
        }

        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedClip != null && TimelineCanvas.IsMouseCaptured)
            {
                var currentPos = e.GetPosition(TimelineCanvas);
                var deltaX = currentPos.X - _lastMousePos.X;

                double newStartX = Math.Max(0, _draggedClip.StartX + deltaX);
                ViewModel.Timeline.UpdateClipTimePosition(_draggedClip, newStartX);

                if (_draggedVisual != null)
                    Canvas.SetLeft(_draggedVisual, _draggedClip.StartX);
                if (_draggedLabel != null)
                    Canvas.SetLeft(_draggedLabel, _draggedClip.StartX + 5);

                _lastMousePos = currentPos;
            }
        }

        private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedClip != null)
                RefreshTracks();

            TimelineCanvas.ReleaseMouseCapture();
            _draggedClip = null;
            _draggedVisual = null;
            _draggedLabel = null;
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
                TimelineScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
        }

        private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
                TrackHeadersScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            if (e.HorizontalChange != 0)
                TimeRulerScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }
}
