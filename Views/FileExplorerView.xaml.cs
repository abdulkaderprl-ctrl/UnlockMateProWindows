using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AdbEasyInstaller.ViewModels;

namespace AdbEasyInstaller.Views
{
    public partial class FileExplorerView : UserControl
    {
        public FileExplorerView()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is FileExplorerViewModel vm && vm.OpenSelectedItemCommand.CanExecute(null))
            {
                vm.OpenSelectedItemCommand.Execute(null);
            }
        }

        private void UserControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && DataContext is FileExplorerViewModel vm)
                {
                    await vm.UploadFilesAsync(files);
                }
            }
        }
    }
}
