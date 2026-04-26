namespace VideoEditorWPF.Interfaces
{
    public interface IDialogService
    {
        string[] ShowOpenFileDialog(string filter, bool multiselect = false);
        string ShowSaveFileDialog(string filter, string defaultFileName);
        void ShowMessage(string message, string title);
    }
}
