using System;
using System.Collections.ObjectModel;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Models
{
    public class Track : ViewModelBase
    {
        private string _name;
        private bool _isSelected;
        private bool _isMuted;
        private bool _isLocked;

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public MediaType Type { get; set; }

        public bool IsDefault { get; set; }

        public int TrackIndex { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (_isMuted != value)
                {
                    _isMuted = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (_isLocked != value)
                {
                    _isLocked = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<Clip> Clips { get; set; }

        public bool CanDelete => !IsDefault;

        public double TopPosition => TrackIndex * 70;

        public Track()
        {
            Clips = new ObservableCollection<Clip>();
        }
    }
}
