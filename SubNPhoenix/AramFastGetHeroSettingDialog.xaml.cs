using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SubNPhoenix
{
    /// <summary>
    /// Interaction logic for AramFastGetHeroSettingDialog.xaml
    /// </summary>
    public partial class AramFastGetHeroSettingDialog : Window
    {
        private readonly AramFastGetHeroSettingDialogViewModel _viewModel;
        public AramFastGetHeroSettingDialog(IEnumerable<Hero> heros)
        {
            InitializeComponent();
            DataContext = _viewModel = new AramFastGetHeroSettingDialogViewModel(heros);
        }

        private void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((Control)sender).Parent as UIElement;
                parent.RaiseEvent(eventArg);
            }
        }

        private async void ListBoxItem_Drop(object sender, DragEventArgs e)
        {
            Hero droppedData = e.Data.GetData(typeof(Hero)) as Hero;
            Hero target = ((ListBoxItem)sender).DataContext as Hero;

            int removedIdx = listbox1.Items.IndexOf(droppedData);
            int targetIdx = listbox1.Items.IndexOf(target);

            if (removedIdx < targetIdx)
            {
                _viewModel.SelectedQuickChooseHeros.Insert(targetIdx + 1, droppedData);
                _viewModel.SelectedQuickChooseHeros.RemoveAt(removedIdx);
            }
            else
            {
                int remIdx = removedIdx + 1;
                if (_viewModel.SelectedQuickChooseHeros.Count + 1 > remIdx)
                {
                    _viewModel.SelectedQuickChooseHeros.Insert(targetIdx, droppedData);
                    _viewModel.SelectedQuickChooseHeros.RemoveAt(remIdx);
                }
            }

            await _viewModel.WriteIntoSetting();
        }

        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            if (sender is ListBoxItem && e.LeftButton == MouseButtonState.Pressed)
            {
                ListBoxItem draggedItem = sender as ListBoxItem;
                DragDrop.DoDragDrop(draggedItem, draggedItem.DataContext, DragDropEffects.Move);
            }
        }
    }
}
