using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UnlockMatePro.Models;
using UnlockMatePro.ViewModels;

namespace UnlockMatePro
{
    public partial class MainWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MenuList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void MenuList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (System.Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    System.Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var listBox = sender as ListBox;
                    if (listBox == null) return;

                    var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (listBoxItem == null) return;

                    var ribbonItem = listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem) as RibbonMenuItem;
                    if (ribbonItem == null) return;

                    _isDragging = true;
                    DataObject dragData = new DataObject("RibbonMenuItemFormat", ribbonItem);
                    DragDrop.DoDragDrop(listBoxItem, dragData, DragDropEffects.Move);
                    _isDragging = false;
                }
            }
        }

        private void MenuList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("RibbonMenuItemFormat"))
            {
                var sourceItem = e.Data.GetData("RibbonMenuItemFormat") as RibbonMenuItem;
                if (sourceItem == null) return;

                var listBox = sender as ListBox;
                if (listBox == null) return;

                var targetContainer = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetContainer == null) return;

                var targetItem = listBox.ItemContainerGenerator.ItemFromContainer(targetContainer) as RibbonMenuItem;
                if (targetItem == null || targetItem == sourceItem) return;

                if (DataContext is MainViewModel vm)
                {
                    int sourceIndex = vm.RibbonItems.IndexOf(sourceItem);
                    int targetIndex = vm.RibbonItems.IndexOf(targetItem);

                    if (sourceIndex >= 0 && targetIndex >= 0)
                    {
                        vm.RibbonItems.Move(sourceIndex, targetIndex);
                        vm.SaveRibbonOrder();
                    }
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }
}

