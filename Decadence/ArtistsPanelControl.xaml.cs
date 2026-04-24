using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Decadence.Models;
using Windows.UI.Xaml.Media;

namespace Decadence
{
    public sealed partial class ArtistsPanelControl : UserControl
    {
        // События для MainPage
        public event EventHandler<ItemClickEventArgs> ArtistClicked;
        public event EventHandler<ItemClickEventArgs> TrackClicked;
        public event EventHandler BackClicked;

        public ArtistsPanelControl()
        {
            this.InitializeComponent();
        }

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

        public bool IsVisible => RootGrid.Visibility == Visibility.Visible;

        public void SetArtists(ObservableCollection<ArtistItem> artists)
        {
            ArtistsList.ItemsSource = artists;
        }

        public void SetTracks(List<TrackItem> tracks, string artistName)
        {
            int i = 1;
            foreach (var track in tracks)
            {
                track.TrackNumber = i++;
            }

            ArtistSongsList.ItemsSource = tracks;
            SelectedArtistName.Text = artistName;
            ShowSongsList();
        }

        private void ShowArtistsList()
        {
            ArtistsList.Visibility = Visibility.Visible;
            ArtistSongsPanel.Visibility = Visibility.Collapsed;
            SelectedArtistName.Visibility = Visibility.Collapsed;
        }

        private void ShowSongsList()
        {
            ArtistsList.Visibility = Visibility.Collapsed;
            ArtistSongsPanel.Visibility = Visibility.Visible;
            SelectedArtistName.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ArtistSongsPanel.Visibility == Visibility.Visible)
            {
                // Сейчас в списке песен → возвращаемся к списку артистов
                ShowArtistsList();
            }
            else
            {
                // Сейчас в списке артистов → закрываем всю панель
                BackClicked?.Invoke(this, EventArgs.Empty);
                Hide();
            }
        }

        private void ArtistsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            ArtistClicked?.Invoke(this, e);
        }

        private void ArtistSongsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            TrackClicked?.Invoke(this, e);
        }
    }
}