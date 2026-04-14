//using System;
//using System.ComponentModel;
//using System.Runtime.CompilerServices;
//using System.Windows.Input;
//using VideoEditorWPF.Commands;
//using VideoEditorWPF.Models;

//namespace VideoEditorWPF.ViewModels
//{
//    public class ClipTrimViewModel : INotifyPropertyChanged
//    {
//        private Clip _clip;

//        public ClipTrimViewModel(Clip clip)
//        {
//            _clip = clip;
//        }

//        public Clip TargetClip
//        {
//            get => _clip;
//            set
//            {
//                _clip = value;
//                OnPropertyChanged();
//                OnPropertyChanged(nameof(TrimStart));
//                OnPropertyChanged(nameof(TrimEnd));
//                OnPropertyChanged(nameof(TrimmedDuration));
//                OnPropertyChanged(nameof(SourceDuration));
//                OnPropertyChanged(nameof(TrimStartPercent));
//                OnPropertyChanged(nameof(TrimEndPercent));
//            }
//        }

//        public double TrimStart
//        {
//            get => _clip?.TrimStart ?? 0;
//            set
//            {
//                if (_clip != null && Math.Abs(_clip.TrimStart - value) > 0.001)
//                {
//                    double newValue = Math.Max(0, Math.Min(value, _clip.TrimEnd - _clip.MinTrimDuration));
//                    _clip.TrimStart = newValue;
//                    _clip.DurationSeconds = _clip.TrimmedDuration;
//                    OnPropertyChanged();
//                    OnPropertyChanged(nameof(TrimStartPercent));
//                    OnPropertyChanged(nameof(TrimmedDuration));
//                }
//            }
//        }

//        public double TrimEnd
//        {
//            get => _clip?.TrimEnd ?? _clip?.SourceDuration ?? 0;
//            set
//            {
//                if (_clip != null && Math.Abs(_clip.TrimEnd - value) > 0.001)
//                {
//                    double newValue = Math.Max(_clip.TrimStart + _clip.MinTrimDuration, Math.Min(value, _clip.SourceDuration));
//                    _clip.TrimEnd = newValue;
//                    _clip.DurationSeconds = _clip.TrimmedDuration;
//                    OnPropertyChanged();
//                    OnPropertyChanged(nameof(TrimEndPercent));
//                    OnPropertyChanged(nameof(TrimmedDuration));
//                }
//            }
//        }

//        public double TrimStartPercent
//        {
//            get => (_clip?.TrimStart ?? 0) / (_clip?.SourceDuration ?? 1) * 100;
//            set => TrimStart = (value / 100) * (_clip?.SourceDuration ?? 1);
//        }

//        public double TrimEndPercent
//        {
//            get => (_clip?.TrimEnd ?? 0) / (_clip?.SourceDuration ?? 1) * 100;
//            set => TrimEnd = (value / 100) * (_clip?.SourceDuration ?? 1);
//        }

//        public double TrimmedDuration => _clip?.TrimmedDuration ?? 0;

//        public double SourceDuration => _clip?.SourceDuration ?? 0;

//        public string TrimStartFormatted => TimeSpan.FromSeconds(TrimStart).ToString(@"hh\:mm\:ss\.fff");

//        public string TrimEndFormatted => TimeSpan.FromSeconds(TrimEnd).ToString(@"hh\:mm\:ss\.fff");

//        public string TrimmedDurationFormatted => TimeSpan.FromSeconds(TrimmedDuration).ToString(@"hh\:mm\:ss\.fff");

//        public event PropertyChangedEventHandler PropertyChanged;

//        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }

//        // VideoEditorWPF/ViewModels/ClipTrimViewModel.cs (дополнения)

//        public ICommand ResetTrimCommand { get; set; }
//        public ICommand FitToPlayheadCommand { get; set; }

//        private void InitializeCommands()
//        {
//            ResetTrimCommand = new RelayCommand(_ => ResetTrim());
//            FitToPlayheadCommand = new RelayCommand(_ => FitToPlayhead());
//        }

//        private void ResetTrim()
//        {
//            if (_clip != null)
//            {
//                _clip.ResetTrim();
//                OnPropertyChanged(nameof(TrimStart));
//                OnPropertyChanged(nameof(TrimEnd));
//                OnPropertyChanged(nameof(TrimmedDuration));
//            }
//        }

//        private void FitToPlayhead()
//        {
//            if (_clip != null)
//            {
//                // Требует доступа к TimelineViewModel для получения позиции плейхеда
//                // Можно реализовать через событие или передачу контекста
//            }
//        }
//    }
//}
// VideoEditorWPF/ViewModels/ClipTrimViewModel.cs
// VideoEditorWPF/ViewModels/ClipTrimViewModel.cs

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VideoEditorWPF.Commands;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.ViewModels
{
    public class ClipTrimViewModel : INotifyPropertyChanged
    {
        private Clip _clip;
        private double _sourceDurationField;  // Переименовано, чтобы избежать конфликта

        // Свойства команд - read-only, инициализируются в конструкторе
        public ICommand ResetTrimCommand { get; }
        public ICommand FitToPlayheadCommand { get; }

        public ClipTrimViewModel(Clip clip)
        {
            _clip = clip;

            // Инициализация команд через backing fields
            ResetTrimCommand = new RelayCommand(_ => ResetTrim());
            FitToPlayheadCommand = new RelayCommand(_ => FitToPlayhead());

            UpdateSourceDuration();
        }

        private void UpdateSourceDuration()
        {
            SourceDuration = _clip?.SourceDuration ?? 0;
        }

        public Clip TargetClip
        {
            get => _clip;
            set
            {
                _clip = value;
                UpdateSourceDuration();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrimStart));
                OnPropertyChanged(nameof(TrimEnd));
                OnPropertyChanged(nameof(TrimmedDuration));
                OnPropertyChanged(nameof(TrimStartPercent));
                OnPropertyChanged(nameof(TrimEndPercent));
            }
        }

        // Удалено дублирующее объявление _sourceDuration
        public double SourceDuration
        {
            get => _sourceDurationField;
            private set
            {
                if (Math.Abs(_sourceDurationField - value) > 0.001)
                {
                    _sourceDurationField = value;
                    OnPropertyChanged();
                }
            }
        }

        public double TrimStart
        {
            get => _clip?.TrimStart ?? 0;
            set
            {
                if (_clip != null && Math.Abs(_clip.TrimStart - value) > 0.001)
                {
                    double newValue = Math.Max(0, Math.Min(value, _clip.TrimEnd - _clip.MinTrimDuration));
                    _clip.TrimStart = newValue;
                    _clip.DurationSeconds = _clip.TrimmedDuration;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TrimStartPercent));
                    OnPropertyChanged(nameof(TrimmedDuration));
                }
            }
        }

        public double TrimEnd
        {
            get => _clip?.TrimEnd ?? 0;
            set
            {
                if (_clip != null && Math.Abs(_clip.TrimEnd - value) > 0.001)
                {
                    double newValue = Math.Max(_clip.TrimStart + _clip.MinTrimDuration, Math.Min(value, _clip.SourceDuration));
                    _clip.TrimEnd = newValue;
                    _clip.DurationSeconds = _clip.TrimmedDuration;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TrimEndPercent));
                    OnPropertyChanged(nameof(TrimmedDuration));
                }
            }
        }

        public double TrimStartPercent
        {
            get => (_clip?.TrimStart ?? 0) / (SourceDuration > 0 ? SourceDuration : 1) * 100;
            set => TrimStart = (value / 100) * SourceDuration;
        }

        public double TrimEndPercent
        {
            get => (_clip?.TrimEnd ?? 0) / (SourceDuration > 0 ? SourceDuration : 1) * 100;
            set => TrimEnd = (value / 100) * SourceDuration;
        }

        public double TrimmedDuration => _clip?.TrimmedDuration ?? 0;

        public string TrimStartFormatted => TimeSpan.FromSeconds(TrimStart).ToString(@"hh\:mm\:ss\.fff");

        public string TrimEndFormatted => TimeSpan.FromSeconds(TrimEnd).ToString(@"hh\:mm\:ss\.fff");

        public string TrimmedDurationFormatted => TimeSpan.FromSeconds(TrimmedDuration).ToString(@"hh\:mm\:ss\.fff");

        private void ResetTrim()
        {
            if (_clip != null)
            {
                _clip.ResetTrim();
                UpdateSourceDuration();
                OnPropertyChanged(nameof(TrimStart));
                OnPropertyChanged(nameof(TrimEnd));
                OnPropertyChanged(nameof(TrimmedDuration));
                OnPropertyChanged(nameof(SourceDuration));
                OnPropertyChanged(nameof(TrimStartPercent));
                OnPropertyChanged(nameof(TrimEndPercent));
            }
        }

        private void FitToPlayhead()
        {
            if (_clip != null)
            {
                // TODO: Требует доступа к TimelineViewModel для получения позиции плейхеда
                // Можно реализовать через событие или передачу контекста
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}