using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Decadence.Models;
using System;

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
            TracksListView.ItemClick += TracksListView_ItemClick;
        }

        // Показать панель
        public void Show()
        {
            RootGrid.Visibility = Visibility.Visible;
        }

        // Скрыть панель
        public void Hide()
        {
            RootGrid.Visibility = Visibility.Collapsed;
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
    }
}