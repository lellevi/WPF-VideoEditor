using System.IO;
using System.Windows;
using Xabe.FFmpeg;

namespace VideoEditorWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            FFmpeg.SetExecutablesPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg"));
        }
    }

}
