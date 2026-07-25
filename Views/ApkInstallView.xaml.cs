using System.Windows;
using System.Windows.Controls;
using AdbEasyInstaller.ViewModels;

namespace AdbEasyInstaller.Views
{
    public partial class ApkInstallView : UserControl
    {
        public ApkInstallView()
        {
            InitializeComponent();
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

        private void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is ApkInstallViewModel vm)
                {
                    _ = vm.AddApkFilesAsync(files);
                }
            }
        }
    }
}
