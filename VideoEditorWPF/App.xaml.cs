using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using VideoEditorWPF.Services;
using Xabe.FFmpeg;

namespace VideoEditorWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string FFmpegFolder { get; private set; } = string.Empty;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ✅ ДИНАМИЧЕСКИЙ путь к выходной папке приложения
            FFmpegFolder = AppDomain.CurrentDomain.BaseDirectory;

            // Проверяем наличие FFmpeg
            if (!Directory.Exists(FFmpegFolder) || !File.Exists(Path.Combine(FFmpegFolder, "ffmpeg.exe")))
            {
                MessageBox.Show($"FFmpeg.exe не найден в {FFmpegFolder}\nСкопируйте ffmpeg.exe в папку с exe", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // Инициализируем Xabe.FFmpeg
            FFmpeg.SetExecutablesPath(FFmpegFolder);
            Debug.WriteLine($"✅ FFmpeg инициализирован: {FFmpegFolder}");
        }
    }


}
