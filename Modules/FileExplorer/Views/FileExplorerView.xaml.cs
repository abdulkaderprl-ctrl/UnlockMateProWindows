using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UnlockMatePro.Models;
using UnlockMatePro.ViewModels;

namespace UnlockMatePro.Views
{
    public partial class FileExplorerView : UserControl
    {
        private Point _dragStartPoint;
        private bool _isDragging = false;

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

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is FileExplorerViewModel vm)
            {
                var selected = FileDataGrid.SelectedItems.Cast<FileItem>();
                vm.SetSelectedItems(selected);
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

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private async void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (DataContext is FileExplorerViewModel vm && FileDataGrid.SelectedItems.Count > 0)
                    {
                        _isDragging = true;
                        await vm.DownloadSelectedItemsAsync();
                    }
                }
            }
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not FileExplorerViewModel vm) return;

            // Ignore shortcuts when editing text inside text boxes
            if (e.OriginalSource is TextBox) return;

            // Ctrl + C (Copy)
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                vm.CopySelectedItems();
                e.Handled = true;
            }
            // Ctrl + X (Cut)
            else if (e.Key == Key.X && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                vm.CutSelectedItems();
                e.Handled = true;
            }
            // Ctrl + V (Paste)
            else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _ = vm.PasteClipboardItemsAsync();
                e.Handled = true;
            }
            // Delete key
            else if (e.Key == Key.Delete)
            {
                _ = vm.DeleteSelectedItemsAsync();
                e.Handled = true;
            }
            // F2 (Rename)
            else if (e.Key == Key.F2)
            {
                if (vm.RenameSingleItemCommand.CanExecute(null)) vm.RenameSingleItemCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void PropertiesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is FileExplorerViewModel vm && vm.SelectedItem != null)
            {
                var win = new FilePropertiesWindow(vm.SelectedItem)
                {
                    Owner = Window.GetWindow(this)
                };
                win.ShowDialog();
            }
        }
    }
}

