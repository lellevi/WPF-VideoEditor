using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace VideoEditorWPF.Models
{
    public class VideoClip : INotifyPropertyChanged
    {
        private string? _filePath;
        public string? FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }

        public string FileName => Path.GetFileName(FilePath ?? string.Empty);
        public double Duration { get; set; }
        public double TrimStart { get; set; }
        public double TrimEnd { get; set; }
        public double LeftPosition { get; set; }
        public double Width { get; set; } = 200;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChangedProtected([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
