using System.Windows;
using UnlockMatePro.Models;

namespace UnlockMatePro.Views
{
    public partial class FilePropertiesWindow : Window
    {
        public FilePropertiesWindow(FileItem item)
        {
            InitializeComponent();
            if (item != null)
            {
                TxtIcon.Text = item.IconBadge;
                TxtItemName.Text = item.Name;
                TxtItemType.Text = $"{item.Type} ({item.FileCategory})";
                TxtFullPath.Text = item.FullPath;
                TxtSize.Text = item.IsDirectory ? "<Directory>" : $"{item.FormattedSize} ({item.SizeBytes:N0} bytes)";
                TxtDate.Text = item.FormattedDate;
                TxtPermissions.Text = item.Permissions;
                TxtOwner.Text = item.Owner;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

