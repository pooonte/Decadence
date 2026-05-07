using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Decadence.Models;
using System;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Input;

namespace Decadence
{
    public sealed partial class FullButtonsPanel : UserControl
    {
        public bool IsVisible => RootGrid.Visibility == Visibility.Visible;
        // Событие при клике на трек
        public event ItemClickEventHandler TrackClicked;
        public event EventHandler BackClicked;

        public FullButtonsPanel()
        {
            this.InitializeComponent();
            TracksListView.ItemClick += TracksListView_ItemClick;
        }

        // Установить список треков
        public void SetTracks(ObservableCollection<TrackItem> tracks)
        {
            TracksListView.ItemsSource = tracks;
        }

        // Показать панель
        // 🔹 ОТКРЫТИЕ
        public void Show()
        {
            RootGrid.Visibility = Visibility.Visible;
            RootGrid.Opacity = 0; // Начальное состояние
            PanelOpenAnimation.Begin();
        }

        // 🔹 ЗАКРЫТИЕ
        public void Hide()
        {
            // Отписываемся от прошлого completed, чтобы не было утечек
            PanelCloseAnimation.Completed -= PanelCloseAnimation_Completed;
            PanelCloseAnimation.Completed += PanelCloseAnimation_Completed;
            PanelCloseAnimation.Begin();
        }

        private void PanelCloseAnimation_Completed(object sender, object e)
        {
            RootGrid.Visibility = Visibility.Collapsed;
            // Сбрасываем Transform на случай повторного открытия
            ((CompositeTransform)RootGrid.RenderTransform).TranslateY = 0;
        }

        private void TracksListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            TrackClicked?.Invoke(this, e);
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            BackClicked?.Invoke(this, EventArgs.Empty);  // ← уведомляем MainPage
        }

        public event EventHandler<TrackItem> AddToPlaylistRequested;

        private void TracksListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var item = (e.OriginalSource as FrameworkElement)?.DataContext as TrackItem;
            if (item != null)
            {
                var menu = new MenuFlyout();
                var addItem = new MenuFlyoutItem { Text = "Добавить в плейлист" };
                addItem.Click += (s, args) => AddToPlaylistRequested?.Invoke(this, item);
                menu.Items.Add(addItem);
                menu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
            }
        }
    }
}