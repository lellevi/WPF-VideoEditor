using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using VideoEditorWPF.Commands;
using VideoEditorWPF.Factories;
using VideoEditorWPF.Models;
using System.Windows;

namespace VideoEditorWPF.ViewModels
{
    public class TimelineViewModel : ViewModelBase
    {
        private const double DefaultScale = 3.0;
        private const double MinScale = 0.5;
        private const double MaxScale = 800.0;  // Increased from 200.0 for better zoom capability

        private double _timelineScale = DefaultScale;
        private double _playheadPosition = 0;
        private bool _isPlaying;
        private Track _selectedTrack;

        private readonly IClipFactory _clipFactory;

        private double _timelineLength = 30.0;
        private double _viewportWidth = 0; // Will be set when window loads

        public ObservableCollection<Track> Tracks { get; }

        public Track SelectedTrack
        {
            get => _selectedTrack;
            set
            {
                if (_selectedTrack != value)
                {
                    if (_selectedTrack != null)
                        _selectedTrack.IsSelected = false;

                    _selectedTrack = value;

                    if (_selectedTrack != null)
                        _selectedTrack.IsSelected = true;

                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
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
                    OnPropertyChanged();
                }
            }
        }

        public void SetMinimumScale(double viewportWidth)
        {
            if (viewportWidth <= 0) return;

            _viewportWidth = viewportWidth;

            // Calculate the minimum scale that fits the entire timeline in viewport
            double dynamicMinScale = viewportWidth / _timelineLength;

            // If current scale is below the minimum, adjust it
            if (_timelineScale < dynamicMinScale)
            {
                TimelineScale = dynamicMinScale;
            }
            else
            {
                // Just trigger a refresh to validate the current scale
                OnPropertyChanged(nameof(TimelineScale));
            }
        }

        public double TimelineScale
        {
            get => _timelineScale;
            set
            {
                // Calculate dynamic minimum scale to fit entire timeline in viewport
                double dynamicMinScale = MinScale;

                if (_viewportWidth > 0 && _timelineLength > 0)
                {
                    dynamicMinScale = Math.Max(MinScale, _viewportWidth / _timelineLength);
                }

                // Clamp value between effective minimum and maximum
                value = Math.Max(dynamicMinScale, Math.Min(MaxScale, value));

                if (Math.Abs(_timelineScale - value) > 0.001) // Use epsilon comparison for doubles
                {
                    _timelineScale = value;
                    OnPropertyChanged();

                    TimelineScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double PlayheadPosition
        {
            get => _playheadPosition;
            set
            {
                if (_playheadPosition != value)
                {
                    _playheadPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public double PlayheadTimeSeconds => PlayheadPosition / TimelineScale;

        // Expose min/max for slider binding
        public double MinTimelineScale => MinScale;
        public double MaxTimelineScale => MaxScale;

        public ICommand AddVideoTrackCommand { get; }
        public ICommand AddAudioTrackCommand { get; }
        public ICommand DeleteSelectedTrackCommand { get; }
        public ICommand ResetPlayheadCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }

        public event EventHandler TimelineScaleChanged;

        public TimelineViewModel(IClipFactory clipFactory)
        {
            _clipFactory = clipFactory;
            Tracks = new ObservableCollection<Track>();

            InitializeDefaultTracks();

            AddVideoTrackCommand = new RelayCommand(_ => AddTrack(MediaType.Video));
            AddAudioTrackCommand = new RelayCommand(_ => AddTrack(MediaType.Audio));
            DeleteSelectedTrackCommand = new RelayCommand(_ => DeleteSelectedTrack());
            ResetPlayheadCommand = new RelayCommand(_ => ResetPlayhead());
            ZoomInCommand = new RelayCommand(_ => ZoomIn());
            ZoomOutCommand = new RelayCommand(_ => ZoomOut());

            Tracks.CollectionChanged += (s, e) => CommandManager.InvalidateRequerySuggested();
        }

        private void InitializeDefaultTracks()
        {
            var videoTrack = new Track
            {
                Name = "Video 1",
                Type = MediaType.Video,
                IsDefault = true,
                TrackIndex = 0
            };
            Tracks.Add(videoTrack);

            var audioTrack = new Track
            {
                Name = "Audio 1",
                Type = MediaType.Audio,
                IsDefault = true,
                TrackIndex = 1
            };
            Tracks.Add(audioTrack);
        }

        public void AddTrack(MediaType trackType)
        {
            var tracksOfType = Tracks.Where(t => t.Type == trackType).ToList();
            int newTrackNumber = tracksOfType.Count + 1;

            var newTrack = new Track
            {
                Name = trackType == MediaType.Video ? $"Video {newTrackNumber}" : $"Audio {newTrackNumber}",
                Type = trackType,
                IsDefault = false
            };

            if (trackType == MediaType.Video)
            {
                Tracks.Insert(0, newTrack);
            }
            else
            {
                Tracks.Add(newTrack);
            }

            ReindexTracks();
        }

        private void DeleteSelectedTrack()
        {
            // Validation checks inline
            if (SelectedTrack == null || SelectedTrack.IsDefault)
                return;

            var tracksOfType = Tracks.Count(t => t.Type == SelectedTrack.Type);
            if (tracksOfType <= 1)
                return;

            // Ask for confirmation
            var result = MessageBox.Show(
                $"Are you sure you want to delete track '{SelectedTrack.Name}'?",
                "Delete Track",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Tracks.Remove(SelectedTrack);
                SelectedTrack = null;
                ReindexTracks();
            }
        }

        private void ReindexTracks()
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                Tracks[i].TrackIndex = i;
            }
            OnPropertyChanged(nameof(CalculatedHeight));
        }

        public void AddClipToTrack(MediaFile mediaFile)
        {
            bool isVideo = !mediaFile.FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) &&
                           !mediaFile.FilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);

            var track = Tracks.FirstOrDefault(t => t.Type == (isVideo ? MediaType.Video : MediaType.Audio));

            if (track == null)
            {
                track = CreateNewTrack(isVideo ? MediaType.Video : MediaType.Audio);
                Tracks.Add(track);
                ReindexTracks();
            }

            AddClipToTrack(track, mediaFile);
        }

        private Track CreateNewTrack(MediaType type)
        {
            int count = Tracks.Count(t => t.Type == type) + 1;
            return new Track
            {
                Name = type == MediaType.Video ? $"Video {count}" : $"Audio {count}",
                Type = type,
                IsDefault = false
            };
        }

        private void AddClipToTrack(Track track, MediaFile mediaFile)
        {
            double startTimeSeconds = PlayheadTimeSeconds;

            var overlappingClip = track.Clips.FirstOrDefault(c =>
                startTimeSeconds >= c.OffsetSeconds &&
                startTimeSeconds < c.OffsetSeconds + c.DurationSeconds);

            if (overlappingClip != null)
            {
                startTimeSeconds = overlappingClip.OffsetSeconds + overlappingClip.DurationSeconds;
            }

            int trackIndex = Tracks.IndexOf(track);
            var clip = _clipFactory.CreateClip(mediaFile, startTimeSeconds, TimelineScale, trackIndex);

            track.Clips.Add(clip);
        }

        public void UpdateClipTimePosition(Clip clip, double newStartX)
        {
            clip.OffsetSeconds = newStartX / TimelineScale;
        }

        public double CalculatedHeight => Tracks.Count * 70; // 70px per track

        private void ResetPlayhead()
        {
            PlayheadPosition = 0;
        }

        private void ZoomIn()
        {
            TimelineScale *= 1.1;
        }

        private void ZoomOut()
        {
            TimelineScale /= 1.1;
        }

        public double TimelineLength
        {
            get => _timelineLength;
            set
            {
                if (_timelineLength != value)
                {
                    _timelineLength = value;
                    OnPropertyChanged();

                    // Revalidate scale when timeline length changes
                    if (_viewportWidth > 0)
                    {
                        // Recalculate minimum and apply if needed
                        double dynamicMinScale = _viewportWidth / _timelineLength;
                        if (_timelineScale < dynamicMinScale)
                        {
                            TimelineScale = dynamicMinScale;
                        }
                    }
                }
            }
        }
    }
}
