using System.Windows;

namespace VideoEditorWPF.Services
{
    public interface IDialogService
    {
        string[] ShowOpenFileDialog(string filter, bool multiselect = false);
        string ShowSaveFileDialog(string filter, string defaultFileName);
        void ShowMessage(string message, string title);
    }

    public class DialogService : IDialogService
    {
        public string[] ShowOpenFileDialog(string filter, bool multiselect = false)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = multiselect,
                Filter = filter
            };

            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        public string ShowSaveFileDialog(string filter, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void ShowMessage(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
// Сервис диалоговых окон для WPF (MVVM-friendly).
// Обертки: OpenFileDialog (multi), SaveFileDialog, MessageBox.
// Возвращает пути файлов или null при отмене.
// Использует Win32 API диалоги Microsoft.Win32.*.
