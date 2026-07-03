using Decadence.Models;
using Decadence.Services;
using Singleton;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Decadence
{
    public sealed partial class PlayerMenu : Page
    {
        public event EventHandler Clicked;

        private readonly List<QueueItem> _queueCache = new List<QueueItem>();

        private bool _userIsSeeking = false;
        private bool _wasPlayingBeforeSeek = false;
        private DispatcherTimer _globalTimer;
        private bool _isActive = false;
        private BitmapImage _playIcon;
        private BitmapImage _pauseIcon;
        private BitmapImage _repeatNoneIcon;
        private BitmapImage _repeatOneIcon;
        private BitmapImage _repeatAllIcon;
        private BitmapImage _currentAlbumArt;

        private double _swipeStartX;
        private readonly double _swipeThreshold = 80;
        private bool _isSwiping = false;

        public PlayerMenu()
        {
            this.InitializeComponent();
            _playIcon = new BitmapImage(new Uri("ms-appx:///Assets/play_white.png"));
            _pauseIcon = new BitmapImage(new Uri("ms-appx:///Assets/pause_white.png"));
            _repeatNoneIcon = new BitmapImage(new Uri("ms-appx:///Assets/repeat_none_white.png"));
            _repeatOneIcon = new BitmapImage(new Uri("ms-appx:///Assets/repeat_one_white.png"));
            _repeatAllIcon = new BitmapImage(new Uri("ms-appx:///Assets/repeat_all_white.png"));
        }

        private string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

        private void GlobalTimer_Tick(object sender, object e)
        {
            if (_isActive) UpdatePlaybackPosition();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isActive = true;

            MediaPlayerSingleton.TrackChanged += MediaPlayerSingleton_TrackChanged;
            MediaPlayerSingleton.PlaybackStateChanged += MediaPlayerSingleton_PlaybackStateChanged;

            var track = MediaPlayerSingleton.CurrentTrack;
            if (track != null)
            {
                await ShowTrackAsync(track);
            }
            else
            {
                FullTrackTitle.Text = "Нет трека";
                FullTrackArtist.Text = "Выберите трек в библиотеке";
            }

            UpdateRepeatButtonIcon();
            UpdateShuffleButtonState();

            if (_globalTimer != null)
            {
                _globalTimer.Stop();
                _globalTimer.Tick -= GlobalTimer_Tick;
            }
            _globalTimer = new DispatcherTimer();
            _globalTimer.Interval = TimeSpan.FromMilliseconds(500);
            _globalTimer.Tick += GlobalTimer_Tick;
            _globalTimer.Start();

            UpdateQueue();
            ForceUpdatePosition();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _isActive = false;
            if (_globalTimer != null)
            {
                _globalTimer.Stop();
                _globalTimer.Tick -= GlobalTimer_Tick;
                _globalTimer = null;
            }

            MediaPlayerSingleton.TrackChanged -= MediaPlayerSingleton_TrackChanged;
            MediaPlayerSingleton.PlaybackStateChanged -= MediaPlayerSingleton_PlaybackStateChanged;

            this.DataContext = null;
            if (QueueListView != null) QueueListView.ItemsSource = null;
            if (FullAlbumArt != null) FullAlbumArt.Source = null;
            _currentAlbumArt = null;

            base.OnNavigatingFrom(e);
        }

        private void PlayerMenu_Unloaded(object sender, RoutedEventArgs e)
        {
            ProgressSlider.Value = 0;
            FullTrackTitle.Text = string.Empty;
            FullTrackArtist.Text = string.Empty;
        }

        // ===== Реакция на общее состояние плеера =====

        private void MediaPlayerSingleton_TrackChanged(object sender, TrackItem track)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!_isActive) return;
                _ = ShowTrackAsync(track);
                UpdateQueue();
                ForceUpdatePosition();
            });
        }

        private void MediaPlayerSingleton_PlaybackStateChanged(object sender, bool isPlaying)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!_isActive) return;
                UpdatePlayButtonState();
            });
        }

        private async Task ShowTrackAsync(TrackItem track)
        {
            FullTrackTitle.Text = track.Title;
            FullTrackArtist.Text = track.Artist;
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "0:00";
            _userIsSeeking = false;

            await LoadAlbumArt(track.FilePath);
            UpdatePlayButtonState();
        }

        // ===== Позиция/время =====

        private void ForceUpdatePosition()
        {
            var session = MediaPlayerSingleton.Player?.PlaybackSession;
            var track = MediaPlayerSingleton.CurrentTrack;

            if (session != null && session.NaturalDuration > TimeSpan.Zero)
            {
                ProgressSlider.Maximum = session.NaturalDuration.TotalSeconds;
                ProgressSlider.Value = session.Position.TotalSeconds;
                CurrentTimeText.Text = FormatTime(session.Position);
                TotalTimeText.Text = FormatTime(session.NaturalDuration);
            }
            else if (track != null && track.Duration > TimeSpan.Zero)
            {
                ProgressSlider.Maximum = track.Duration.TotalSeconds;
                TotalTimeText.Text = FormatTime(track.Duration);
            }
        }

        private void UpdatePlaybackPosition()
        {
            var session = MediaPlayerSingleton.Player?.PlaybackSession;
            if (session != null && session.NaturalDuration > TimeSpan.Zero && FullTrackTitle != null)
            {
                ProgressSlider.Maximum = session.NaturalDuration.TotalSeconds;
                TotalTimeText.Text = FormatTime(session.NaturalDuration);

                if (!_userIsSeeking)
                {
                    ProgressSlider.Value = session.Position.TotalSeconds;
                    CurrentTimeText.Text = FormatTime(session.Position);
                }
            }
        }

        private async Task LoadAlbumArt(string filePath)
        {
            try
            {
                if (_currentAlbumArt != null)
                {
                    FullAlbumArt.Source = null;
                    _currentAlbumArt = null;
                }

                var file = await StorageFile.GetFileFromPathAsync(filePath);
                using (var thumb = await file.GetThumbnailAsync(
                    Windows.Storage.FileProperties.ThumbnailMode.MusicView, 128))
                {
                    if (thumb != null && thumb.Size > 0)
                    {
                        _currentAlbumArt = new BitmapImage
                        {
                            CreateOptions = BitmapCreateOptions.IgnoreImageCache
                        };
                        await _currentAlbumArt.SetSourceAsync(thumb);
                        FullAlbumArt.Source = _currentAlbumArt;
                    }
                }
            }
            catch { }
        }

        private void UpdatePlayButtonState()
        {
            if (FullPlayPauseIcon != null)
                FullPlayPauseIcon.Source = MediaPlayerSingleton.IsPlaying ? _pauseIcon : _playIcon;
        }

        private void UpdateRepeatButtonIcon()
        {
            switch (MediaPlayerSingleton.RepeatMode)
            {
                case RepeatMode.None: RepeatButtonImage.Source = _repeatNoneIcon; break;
                case RepeatMode.One: RepeatButtonImage.Source = _repeatOneIcon; break;
                case RepeatMode.All: RepeatButtonImage.Source = _repeatAllIcon; break;
            }
        }

        private void UpdateShuffleButtonState()
        {
            if (ShuffleButton.Content is TextBlock textBlock)
                textBlock.Text = MediaPlayerSingleton.IsShuffleEnabled ? "Перемешать ✓" : "Перемешать";
        }

        // ===== Управление воспроизведением =====

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.TogglePlayPause();
            UpdatePlayButtonState();
        }

        private async void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            await MediaPlayerSingleton.PreviousAsync();
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await MediaPlayerSingleton.NextAsync();
        }

        private void ProgressSlider_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _userIsSeeking = true;
            _wasPlayingBeforeSeek = MediaPlayerSingleton.IsPlaying;
            if (_wasPlayingBeforeSeek)
            {
                MediaPlayerSingleton.Player.Pause();
                UpdatePlayButtonState();
            }
        }

        private async void ProgressSlider_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            _userIsSeeking = false;
            var session = MediaPlayerSingleton.Player.PlaybackSession;
            if (session == null) return;

            double remainingTime = session.NaturalDuration.TotalSeconds - ProgressSlider.Value;

            if (remainingTime < 2 && ProgressSlider.Value > 0)
            {
                await MediaPlayerSingleton.NextAsync();
            }
            else
            {
                session.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
                if (_wasPlayingBeforeSeek)
                {
                    MediaPlayerSingleton.Player.Play();
                    UpdatePlayButtonState();
                }
            }
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            switch (MediaPlayerSingleton.RepeatMode)
            {
                case RepeatMode.None:
                    MediaPlayerSingleton.RepeatMode = RepeatMode.One;
                    break;
                case RepeatMode.One:
                    MediaPlayerSingleton.RepeatMode = RepeatMode.All;
                    break;
                case RepeatMode.All:
                    MediaPlayerSingleton.RepeatMode = RepeatMode.None;
                    break;
            }
            UpdateRepeatButtonIcon();
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.ToggleShuffle();
            UpdateShuffleButtonState();
            UpdateQueue();
        }

        // ===== Инфо / плейлисты =====

        private async void TrackInfo_Click(object sender, RoutedEventArgs e)
        {
            var track = MediaPlayerSingleton.CurrentTrack;
            if (track == null) return;

            var dialog = new MessageDialog(
                $"Название: {track.Title}\n" +
                $"Исполнитель: {track.Artist}\n" +
                $"Альбом: {track.Album}\n" +
                $"Длительность: {track.Duration:mm\\:ss}\n" +
                $"Путь: {track.FilePath}",
                "Информация о треке"
            );
            await dialog.ShowAsync();
        }

        private async void AddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var track = MediaPlayerSingleton.CurrentTrack;
            if (track == null)
            {
                var dialog = new MessageDialog("Нет активного трека");
                await dialog.ShowAsync();
                return;
            }

            var playlists = App.CurrentPlaylists;
            if (playlists == null || playlists.Count == 0)
            {
                var dialog = new MessageDialog("Нет плейлистов. Создайте первый в главном меню.");
                await dialog.ShowAsync();
                return;
            }

            var options = playlists.Select(p => p.Name).ToArray();
            var selectedName = await ShowPlaylistPicker(options);

            if (selectedName != null)
            {
                var playlist = playlists.First(p => p.Name == selectedName);
                if (!playlist.Tracks.Any(t => t.FilePath == track.FilePath))
                {
                    playlist.Tracks.Add(track);
                    await PlaylistStorage.SavePlaylistsAsync(playlists);

                    var dialog = new MessageDialog($"Трек добавлен в плейлист \"{playlist.Name}\"");
                    await dialog.ShowAsync();

                    App.NotifyPlaylistsUpdated();
                }
                else
                {
                    var dialog = new MessageDialog("Трек уже есть в этом плейлисте");
                    await dialog.ShowAsync();
                }
            }
        }

        private async Task<string> ShowPlaylistPicker(string[] options)
        {
            var dialog = new ContentDialog
            {
                Title = "Выберите плейлист",
                PrimaryButtonText = "Добавить",
                SecondaryButtonText = "Отмена"
            };

            var listBox = new ListBox();
            foreach (var opt in options) listBox.Items.Add(opt);
            if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
            dialog.Content = listBox;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && listBox.SelectedItem != null)
                return listBox.SelectedItem.ToString();
            return null;
        }

        private void MenuToggleButton_Click(object sender, RoutedEventArgs e)
        {
            MenuSlidePanel.Visibility = Visibility.Visible;
        }

        private void CloseMenuPanel_Click(object sender, RoutedEventArgs e)
        {
            MenuSlidePanel.Visibility = Visibility.Collapsed;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private void RootButton_Click(object sender, RoutedEventArgs e)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }

        // ===== Очередь =====

        private void UpdateQueue()
        {
            _queueCache.Clear();
            var playlist = MediaPlayerSingleton.CurrentPlaylist;

            for (int i = 0; i < playlist.Count; i++)
            {
                _queueCache.Add(new QueueItem
                {
                    Index = i + 1,
                    Title = playlist[i].Title,
                    Artist = playlist[i].Artist
                });
            }

            QueueListView.ItemsSource = null;
            QueueListView.ItemsSource = _queueCache;
        }

        private async void QueueListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is QueueItem item)
            {
                int index = item.Index - 1;
                var playlist = MediaPlayerSingleton.CurrentPlaylist;
                if (index >= 0 && index < playlist.Count)
                {
                    await MediaPlayerSingleton.PlayAsync(playlist[index]);
                }
            }
        }

        // ===== Свайп по обложке =====

        private void AlbumArt_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _swipeStartX = e.Position.X;
            _isSwiping = true;
        }

        private void AlbumArt_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (!_isSwiping) return;

            double deltaX = e.Cumulative.Translation.X;

            if (deltaX > 30)
            {
                SwipeRightHint.Opacity = Math.Min(1, deltaX / _swipeThreshold);
                SwipeLeftHint.Opacity = 0;
            }
            else if (deltaX < -30)
            {
                SwipeLeftHint.Opacity = Math.Min(1, -deltaX / _swipeThreshold);
                SwipeRightHint.Opacity = 0;
            }
            else
            {
                SwipeLeftHint.Opacity = 0;
                SwipeRightHint.Opacity = 0;
            }
        }

        private async void AlbumArt_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            _isSwiping = false;
            SwipeLeftHint.Opacity = 0;
            SwipeRightHint.Opacity = 0;

            double totalDeltaX = e.Cumulative.Translation.X;

            if (Math.Abs(totalDeltaX) > _swipeThreshold)
            {
                if (totalDeltaX > 0)
                    await MediaPlayerSingleton.PreviousAsync();
                else
                    await MediaPlayerSingleton.NextAsync();
            }
        }

        private async void AlbumArt_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var track = MediaPlayerSingleton.CurrentTrack;
            if (track == null) return;

            var menu = new MenuFlyout();

            var favoriteItem = new MenuFlyoutItem
            {
                Text = track.IsFavorite ? "Убрать из избранного" : "В избранное"
            };
            favoriteItem.Click += async (s, args) =>
            {
                bool nowFavorite = await LibraryDatabase.ToggleFavoriteAsync(track.Id);
                track.IsFavorite = nowFavorite;
            };
            menu.Items.Add(favoriteItem);

            menu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
        }
    }
}