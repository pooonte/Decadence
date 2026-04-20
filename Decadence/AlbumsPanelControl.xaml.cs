using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Decadence.Models;

namespace Decadence
{
    public sealed partial class AlbumsPanelControl : UserControl
    {
        public event EventHandler<ItemClickEventArgs> AlbumClicked;
        public event EventHandler<ItemClickEventArgs> TrackClicked;
        public event EventHandler BackClicked;

        public AlbumsPanelControl()
        {
            this.InitializeComponent();
        }

        public void Show()
        {
            RootGrid.Visibility = Visibility.Visible;
            ShowAlbumsList();
        }

        public void Hide()
        {
            RootGrid.Visibility = Visibility.Collapsed;
        }

        public bool IsVisible => RootGrid.Visibility == Visibility.Visible;

        public void SetAlbums(ObservableCollection<AlbumItem> albums)
        {
            AlbumsList.ItemsSource = albums;
        }

        public void SetTracks(List<TrackItem> tracks, string albumName)
        {
            int i = 1;
            foreach (var track in tracks)
            {
                track.TrackNumber = i++;
            }

            AlbumSongsList.ItemsSource = tracks;
            SelectedAlbumName.Text = albumName;
            ShowSongsList();
        }

        private void ShowAlbumsList()
        {
            AlbumsList.Visibility = Visibility.Visible;
            AlbumSongsPanel.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Collapsed;
            SelectedAlbumName.Visibility = Visibility.Collapsed;
        }

        private void ShowSongsList()
        {
            AlbumsList.Visibility = Visibility.Collapsed;
            AlbumSongsPanel.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;
            SelectedAlbumName.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (AlbumSongsPanel.Visibility == Visibility.Visible)
            {
                ShowAlbumsList();
            }
            else
            {
                BackClicked?.Invoke(this, EventArgs.Empty);
                Hide();
            }
        }

        private void AlbumsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            AlbumClicked?.Invoke(this, e);
        }

        private void AlbumSongsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            TrackClicked?.Invoke(this, e);
        }
    }
}