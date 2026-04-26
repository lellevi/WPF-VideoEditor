using System.Windows;
using VideoEditorWPF.Interfaces;

namespace VideoEditorWPF.Services
{
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
