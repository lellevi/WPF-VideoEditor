using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private Point _lastMousePos;
        private Clip _draggedClip;
        private Rectangle _draggedVisual;
        private TextBlock _draggedLabel;

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
            var mainViewModel = new MainViewModel(mediaService, dialogService, timelineService, timelineViewModel);

            IClipRenderService clipRenderService = new ClipRenderService();
            _trackRenderService = new TrackRenderService(clipRenderService);
            _timelineRenderService = new TimelineRenderService();

            DataContext = mainViewModel;
            SetupEventHandlers();
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

            Loaded += Window_Loaded;
            Closing += Window_Closing;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshTimeline();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TimelineScrollViewer.MouseWheel -= TimelineScrollViewer_MouseWheel;

            ViewModel.Timeline.Tracks.CollectionChanged -= Tracks_CollectionChanged;
            foreach (var track in ViewModel.Timeline.Tracks)
            {
                track.Clips.CollectionChanged -= Clips_CollectionChanged;
            }

            ViewModel.Timeline.TimelineScaleChanged -= TimelineScaleChanged_Handler;

            Loaded -= Window_Loaded;
            Closing -= Window_Closing;
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

        private void MediaLibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MediaLibraryList.SelectedItem is MediaFile mediaFile)
            {
                ViewModel.Timeline.AddClipToTrack(mediaFile);
            }
        }

        private void RefreshTimeline()
        {
            _timelineRenderService.ClearTimeline(TimelineCanvas);
            _timelineRenderService.RenderTimeline(TimelineCanvas, ViewModel.Timeline.TimelineScale, TimelineScrollViewer.ViewportWidth);
            RefreshTracks();
        }

        private void RefreshTracks()
        {
            _trackRenderService.ClearTracks(TimelineCanvas);
            _trackRenderService.RenderTracks(TimelineCanvas, ViewModel.Timeline.Tracks, TimelineScrollViewer.ViewportWidth);
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

                double newStartX = System.Math.Max(0, _draggedClip.StartX + deltaX);
                ViewModel.Timeline.UpdateClipTimePosition(_draggedClip, newStartX);

                if (_draggedVisual != null)
                {
                    Canvas.SetLeft(_draggedVisual, _draggedClip.StartX);
                }

                if (_draggedLabel != null)
                {
                    Canvas.SetLeft(_draggedLabel, _draggedClip.StartX + 5);
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

        private void TrackHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Track track)
            {
                ViewModel.Timeline.SelectedTrack = track;
                e.Handled = true;
            }
        }
    }
}
