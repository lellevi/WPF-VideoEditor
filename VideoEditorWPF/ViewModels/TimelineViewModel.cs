using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VideoEditorWPF.Commands;
using VideoEditorWPF.Factories;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.ViewModels
{
    public class TimelineViewModel : ViewModelBase
    {
        private const double DefaultScale = 3.0;
        private const double MinScale = 0.5;
        private const double MaxScale = 200.0;

        private double _timelineScale = DefaultScale;
        private double _playheadPosition = 0;
        private bool _isPlaying;
        private Track _selectedTrack;

        private readonly IClipFactory _clipFactory;

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

        public double TimelineScale
        {
            get => _timelineScale;
            set
            {
                value = Math.Max(MinScale, Math.Min(MaxScale, value));
                if (_timelineScale != value)
                {
                    _timelineScale = value;
                    OnPropertyChanged();
                    UpdateAllClipPositionsAndWidths();
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
                    _playheadPosition = Math.Max(0, value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlayheadSeconds));
                    OnPropertyChanged(nameof(CurrentTimeString));
                    OnPropertyChanged(nameof(TotalDurationSeconds));
                }
            }
        }

        public double PlayheadSeconds
        {
            get => PlayheadPosition / TimelineScale;
            set
            {
                PlayheadPosition = value * TimelineScale;
            }
        }

        public string CurrentTimeString => TimeSpan.FromSeconds(PlayheadSeconds).ToString(@"hh\:mm\:ss");
        public double TotalDurationSeconds => GetTotalDuration().TotalSeconds;
        public string TotalDurationString => GetTotalDuration().ToString(@"hh\:mm\:ss");

        public ICommand AddVideoTrackCommand { get; }
        public ICommand AddAudioTrackCommand { get; }
        public ICommand DeleteSelectedTrackCommand { get; }
        public ICommand ResetPlayheadCommand { get; }

        public event EventHandler TimelineScaleChanged;

        public TimelineViewModel(IClipFactory clipFactory)
        {
            _clipFactory = clipFactory;
            Tracks = new ObservableCollection<Track>();

            InitializeDefaultTracks();

            AddVideoTrackCommand = new RelayCommand(_ => AddTrack(MediaType.Video));
            AddAudioTrackCommand = new RelayCommand(_ => AddTrack(MediaType.Audio));
            DeleteSelectedTrackCommand = new RelayCommand(_ => DeleteSelectedTrack(),
                _ => SelectedTrack != null && !SelectedTrack.IsDefault && CanDeleteTrack());
            ResetPlayheadCommand = new RelayCommand(_ => ResetPlayhead());

            Tracks.CollectionChanged += (s, e) => {
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(CalculatedHeight));
                OnPropertyChanged(nameof(TotalDurationSeconds));
                OnPropertyChanged(nameof(TotalDurationString));
            };
        }

        private bool CanDeleteTrack()
        {
            return SelectedTrack != null &&
                   !SelectedTrack.IsDefault &&
                   Tracks.Count(t => t.Type == SelectedTrack.Type) > 1;
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
                Tracks.Insert(0, newTrack);
            else
                Tracks.Add(newTrack);

            ReindexTracks();
        }

        private void DeleteSelectedTrack()
        {
            if (SelectedTrack == null || SelectedTrack.IsDefault) return;

            var tracksOfType = Tracks.Count(t => t.Type == SelectedTrack.Type);
            if (tracksOfType <= 1) return;

            var result = MessageBox.Show(
                $"Delete track '{SelectedTrack.Name}'?",
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

        public double CalculatedHeight => Tracks.Count * 70;

        public double PlayheadTimeSeconds => PlayheadPosition / TimelineScale;

        private void ResetPlayhead()
        {
            PlayheadPosition = 0;
        }

        public TimeSpan GetTotalDuration()
        {
            double maxEnd = 0;

            foreach (var track in Tracks)
            {

                foreach (var clip in track.Clips)
                {
                    double endTime = clip.StartTimeSeconds + clip.DurationSeconds;

                    if (endTime > maxEnd) maxEnd = endTime;
                }
            }

            var duration = TimeSpan.FromSeconds(maxEnd);
            return duration;
        }
    }
}
// ViewModel timeline с треками Video/Audio (по умолчанию 1+1).
// Управляет масштабом (0.5-200 px/s), playhead, добавлением/удалением треков.
// Автоматическое размещение клипов без перекрытия. Синхронизирует StartX/Width.
// Вычисляет TotalDuration по max end всех клипов. ReindexTracks при изменениях.
