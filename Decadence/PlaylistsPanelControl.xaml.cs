using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Decadence.Models;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Input;

namespace Decadence
{
    public sealed partial class PlaylistsPanelControl : UserControl
    {
        public event EventHandler<Playlist> PlaylistSelected;
        public event EventHandler<Playlist> PlaylistEditRequested;
        public event EventHandler<PlaylistTrackEventArgs> RemoveTrackRequested;

        private List<Playlist> _playlists;

        public PlaylistsPanelControl()
        {
            this.InitializeComponent();
        }

        public void Show()
        {
            RootGrid.Visibility = Visibility.Visible;
            RootGrid.Opacity = 0;
            PanelOpenAnimation.Begin();
        }

        public void Hide()
        {
            PanelCloseAnimation.Completed -= PanelCloseAnimation_Completed;
            PanelCloseAnimation.Completed += PanelCloseAnimation_Completed;
            PanelCloseAnimation.Begin();
        }

        private void PanelCloseAnimation_Completed(object sender, object e)
        {
            RootGrid.Visibility = Visibility.Collapsed;
            ((CompositeTransform)RootGrid.RenderTransform).TranslateY = 0;
        }

        public bool IsVisible => RootGrid.Visibility == Visibility.Visible;

        public void SetPlaylists(List<Playlist> playlists)
        {
            _playlists = playlists;
            PlaylistsList.ItemsSource = null;      // сброс
            PlaylistsList.ItemsSource = playlists; // установка
        }

        private void Playlist_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Playlist playlist)
            {
                PlaylistSelected?.Invoke(this, playlist);
                Hide();
            }
        }

        private void PlaylistMenu_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var playlist = button?.Tag as Playlist;
            if (playlist != null)
            {
                PlaylistEditRequested?.Invoke(this, playlist);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void PlaylistTracksList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var track = (e.OriginalSource as FrameworkElement)?.DataContext as TrackItem;
            var playlist = (sender as ListView)?.Tag as Playlist;
            if (track != null && playlist != null)
            {
                var menu = new MenuFlyout();
                var removeItem = new MenuFlyoutItem { Text = "Удалить из плейлиста" };
                removeItem.Click += (s, args) => RemoveTrackRequested?.Invoke(this, new PlaylistTrackEventArgs
                {
                    Playlist = playlist,
                    Track = track
                });
                menu.Items.Add(removeItem);
                menu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
            }
        }

        private async void CreatePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Windows.UI.Popups.MessageDialog("Введите название плейлиста", "Создать");
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Создать") { Id = 0 });
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Отмена") { Id = 1 });
            dialog.DefaultCommandIndex = 0;

            var result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                // Нужно получить текст — через TextBox в ContentDialog
                var inputDialog = new ContentDialog
                {
                    Title = "Название плейлиста",
                    Content = new TextBox { PlaceholderText = "Мой плейлист" },
                    PrimaryButtonText = "Создать",
                    SecondaryButtonText = "Отмена"
                };
                if (await inputDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    string name = (inputDialog.Content as TextBox)?.Text;
                    if (!string.IsNullOrWhiteSpace(name))
                        CreatePlaylist?.Invoke(this, name);
                }
            }
        }

        public event EventHandler<string> CreatePlaylist;
    }
}