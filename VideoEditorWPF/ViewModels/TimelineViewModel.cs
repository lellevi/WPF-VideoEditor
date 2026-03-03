using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoEditorWPF.Factories;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.ViewModels
{
    public class TimelineViewModel : ViewModelBase
    {
        private const double DEFAULT_SCALE = 3.0;
        private const double MIN_SCALE = 0.5;
        private const double MAX_SCALE = 200.0;

        private double _timelineScale = DEFAULT_SCALE;
        private double _playheadPosition = 0;
        private bool _isPlaying;

        private readonly IClipFactory _clipFactory;

        public ObservableCollection<Track> Tracks { get; }

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

        public double TimelineScale
        {
            get => _timelineScale;
            set
            {
                value = System.Math.Max(MIN_SCALE, System.Math.Min(MAX_SCALE, value));
                if (_timelineScale != value)
                {
                    _timelineScale = value;
                    OnPropertyChanged();
                    UpdateAllClipPositionsAndWidths();
                    TimelineScaleChanged?.Invoke(this, System.EventArgs.Empty);
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

        public event System.EventHandler TimelineScaleChanged;

        public TimelineViewModel(IClipFactory clipFactory)
        {
            _clipFactory = clipFactory;
            Tracks = new ObservableCollection<Track>
            {
                new Track { Name = "Video 1", Type = TrackType.Video },
                new Track { Name = "Audio 1", Type = TrackType.Audio }
            };
        }

        public void AddClipToTrack(MediaFile mediaFile)
        {
            bool isVideo = !mediaFile.FilePath.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase) &&
                           !mediaFile.FilePath.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase);

            var track = Tracks.FirstOrDefault(t => t.Type == (isVideo ? TrackType.Video : TrackType.Audio));

            if (track == null)
            {
                track = CreateNewTrack(isVideo ? TrackType.Video : TrackType.Audio);
                Tracks.Add(track);
            }

            AddClipToTrack(track, mediaFile);
        }

        private Track CreateNewTrack(TrackType type)
        {
            int count = Tracks.Count(t => t.Type == type) + 1;
            return new Track
            {
                Name = type == TrackType.Video ? $"Video {count}" : $"Audio {count}",
                Type = type
            };
        }

        private void AddClipToTrack(Track track, MediaFile mediaFile)
        {
            double startTimeSeconds = PlayheadTimeSeconds;

            var overlappingClip = track.Clips.FirstOrDefault(c =>
                startTimeSeconds >= c.StartTimeSeconds &&
                startTimeSeconds < c.StartTimeSeconds + c.DurationSeconds);

            if (overlappingClip != null)
            {
                startTimeSeconds = overlappingClip.StartTimeSeconds + overlappingClip.DurationSeconds;
            }

            int trackIndex = Tracks.IndexOf(track);
            var clip = _clipFactory.CreateClip(mediaFile, startTimeSeconds, TimelineScale, trackIndex);

            track.Clips.Add(clip);

            PlayheadPosition = (startTimeSeconds + mediaFile.Duration.TotalSeconds) * TimelineScale;
        }

        private void UpdateAllClipPositionsAndWidths()
        {
            foreach (var track in Tracks)
            {
                foreach (var clip in track.Clips)
                {
                    clip.StartX = clip.StartTimeSeconds * TimelineScale;
                    clip.Width = clip.DurationSeconds * TimelineScale;
                }
            }
        }

        public void UpdateClipTimePosition(Clip clip, double newStartX)
        {
            clip.StartX = newStartX;
            clip.StartTimeSeconds = newStartX / TimelineScale;
        }
    }
}
