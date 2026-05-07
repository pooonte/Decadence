using Decadence.Models;
using Decadence.Services;
using Singleton;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Decadence
{
    public sealed partial class PlayerMenu : Page
    {
        private List<TrackItem> _currentPlaylist = new List<TrackItem>();
        private int _currentPlaylistIndex = -1;

        public event EventHandler Clicked;
        private TrackItem currentTrack;
        private bool _userIsSeeking = false;
        private bool _wasPlayingBeforeSeek = false;
        private RepeatMode _repeatMode = RepeatMode.None;
        private static DispatcherTimer _globalTimer;
        private bool _isActive = false;
        private BitmapImage _playIcon;
        private BitmapImage _pauseIcon;

        public PlayerMenu()
        {
            this.InitializeComponent();

            _playIcon = new BitmapImage(new Uri("ms-appx:///Assets/play.png"));
            _pauseIcon = new BitmapImage(new Uri("ms-appx:///Assets/pause.png"));

            MediaPlayerSingleton.Player.MediaEnded += Player_MediaEnded;

        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isActive = true;
            App.PlayerMenuInstance = this;

            // Загружаем данные
            if (App.CurrentTrack != null && (e.Parameter == null || !(e.Parameter is FullPlayerNavigationData)))
            {
                currentTrack = App.CurrentTrack;
                _currentPlaylist = App.CurrentPlaylist ?? new List<TrackItem>();
                _currentPlaylistIndex = App.CurrentPlaylistIndex;
                _repeatMode = App.CurrentRepeatMode;

                FullTrackTitle.Text = currentTrack.Title;
                FullTrackArtist.Text = currentTrack.Artist;
                await LoadAlbumArt(currentTrack.FilePath);
                UpdatePlayPauseButton();
            }
            else if (e.Parameter is FullPlayerNavigationData data && data.Track != null)
            {
                currentTrack = data.Track;
                _currentPlaylist = data.Playlist ?? new List<TrackItem>();
                _currentPlaylistIndex = data.PlaylistIndex;
                _repeatMode = data.CurrentRepeatMode;

                FullTrackTitle.Text = currentTrack.Title;
                FullTrackArtist.Text = currentTrack.Artist;
                await LoadAlbumArt(currentTrack.FilePath);
                UpdatePlayPauseButton();
            }
            else
            {
                FullTrackTitle.Text = "Нет трека";
                FullTrackArtist.Text = "Выберите трек в библиотеке";
            }

            if (_globalTimer != null)
            {
                _globalTimer.Stop();
                _globalTimer = null;
            }

            _globalTimer = new DispatcherTimer();
            _globalTimer.Interval = TimeSpan.FromMilliseconds(500);
            _globalTimer.Tick += (s, args) =>
            {
                if (_isActive)
                {
                    UpdatePlaybackPosition();
                }
            };
            _globalTimer.Start();

            UpdateQueue();
            // Принудительно обновляем позицию
            ForceUpdatePosition();
        }
        // Добавь этот метод в класс PlayerMenu
        private void ForceUpdatePosition()
        {
            var session = MediaPlayerSingleton.Player?.PlaybackSession;
            if (session != null && session.NaturalDuration > TimeSpan.Zero)
            {
                ProgressSlider.Maximum = session.NaturalDuration.TotalSeconds;
                ProgressSlider.Value = session.Position.TotalSeconds;
                CurrentTimeText.Text = FormatTime(session.Position);
                TotalTimeText.Text = FormatTime(session.NaturalDuration);
                System.Diagnostics.Debug.WriteLine($"ForceUpdatePosition: {session.Position.TotalSeconds}/{session.NaturalDuration.TotalSeconds}");
            }
            else if (currentTrack?.Duration != null && currentTrack.Duration > TimeSpan.Zero)
            {
                ProgressSlider.Maximum = currentTrack.Duration.TotalSeconds;
                TotalTimeText.Text = FormatTime(currentTrack.Duration);
                System.Diagnostics.Debug.WriteLine($"ForceUpdatePosition from track info: {currentTrack.Duration.TotalSeconds}");
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
                    var newValue = session.Position.TotalSeconds;
                    if (Math.Abs(ProgressSlider.Value - newValue) > 0.1) // Отладка
                    {
                        System.Diagnostics.Debug.WriteLine($"UpdatePlaybackPosition: {newValue}/{session.NaturalDuration.TotalSeconds}");
                    }
                    ProgressSlider.Value = newValue;
                    CurrentTimeText.Text = FormatTime(session.Position);
                }
            }
        }
        private async Task LoadAlbumArt(string filePath)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                var thumb = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.MusicView, 256);
                if (thumb != null && thumb.Size > 0)
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(thumb);
                    FullAlbumArt.Source = bitmap;
                }
            }
            catch { }
        }

        private void UpdatePlayPauseButton()
        {
            if (FullPlayPauseIcon != null)
            {
                bool isPlaying = MediaPlayerSingleton.IsPlaying;
                string iconName = isPlaying ? "pause.png" : "play.png";
                FullPlayPauseIcon.Source = new BitmapImage(new Uri($"ms-appx:///Assets/{iconName}"));
            }
        }
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            _isActive = false;
            if (_globalTimer != null)
            {
                _globalTimer.Stop();
                _globalTimer = null;
            }
        }

        private async void PlayTrack(StorageFile file)
        {
            if (file == null) return;

            System.Diagnostics.Debug.WriteLine($"PlayTrack: {file.Path}");

            var track = _currentPlaylist.FirstOrDefault(t => t.FilePath == file.Path);
            if (track == null)
            {
                track = App.Tracks?.FirstOrDefault(t => t.FilePath == file.Path);
                if (track == null) return;

                _currentPlaylist = App.Tracks.ToList();
                _currentPlaylistIndex = _currentPlaylist.IndexOf(track);
            }
            else
            {
                _currentPlaylistIndex = _currentPlaylist.IndexOf(track);
            }

            currentTrack = track;
            MediaPlayerSingleton.PlayFile(file);

            // Обновляем UI
            FullTrackTitle.Text = track.Title;
            FullTrackArtist.Text = track.Artist;
            System.Diagnostics.Debug.WriteLine($"UI обновлен: {track.Title} - {track.Artist}");

            App.CurrentTrack = currentTrack;
            App.CurrentPlaylist = _currentPlaylist;
            App.CurrentPlaylistIndex = _currentPlaylistIndex;
            App.CurrentRepeatMode = _repeatMode;

            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "0:00";
            _userIsSeeking = false;

            var saved = ApplicationData.Current.LocalSettings.Values["SavedVolume"];
            MediaPlayerSingleton.Player.Volume = saved is double v ? v : 1.0;

            try
            {
                var thumb = await file.GetThumbnailAsync(
                    Windows.Storage.FileProperties.ThumbnailMode.MusicView, 256);
                if (thumb != null && thumb.Size > 0)
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(thumb);
                    FullAlbumArt.Source = bitmap;
                }
            }
            catch { }

            UpdatePlayButtonState();
        }

        private void UpdatePlayButtonState()
        {
            if (FullPlayPauseIcon != null)
            {
                FullPlayPauseIcon.Source = MediaPlayerSingleton.IsPlaying ? _pauseIcon : _playIcon;
            }
        }

        private void UpdateRepeatButtonIcon()
        {
            switch (_repeatMode)
            {
                case RepeatMode.None:
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_none_white.png"));
                    break;
                case RepeatMode.One:
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_one_white.png"));
                    break;
                case RepeatMode.All:
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_all_white.png"));
                    break;
            }
        }

        private string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

        private async Task PlayNextTrack()
        {
            System.Diagnostics.Debug.WriteLine($"PlayNextTrack вызван. CurrentPlaylistIndex: {_currentPlaylistIndex}, Playlist.Count: {_currentPlaylist.Count}");

            if (_currentPlaylist.Count == 0) return;

            if (_repeatMode == RepeatMode.One)
            {
                if (currentTrack != null)
                {
                    var trackFile = await StorageFile.GetFileFromPathAsync(currentTrack.FilePath);
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => PlayTrack(trackFile));
                }
                return;
            }

            int nextIndex = _currentPlaylistIndex + 1;
            System.Diagnostics.Debug.WriteLine($"nextIndex: {nextIndex}");

            if (nextIndex >= _currentPlaylist.Count)
            {
                if (_repeatMode == RepeatMode.None)
                {
                    MediaPlayerSingleton.Player.Pause();
                    System.Diagnostics.Debug.WriteLine("Конец списка, повтор выключен - остановка");
                    return;
                }
                else if (_repeatMode == RepeatMode.All)
                {
                    nextIndex = 0;
                    System.Diagnostics.Debug.WriteLine("Конец списка, повтор всех - начало с начала");
                }
            }

            var nextTrack = _currentPlaylist[nextIndex];
            System.Diagnostics.Debug.WriteLine($"Следующий трек: {nextTrack.Title}");

            var nextFile = await StorageFile.GetFileFromPathAsync(nextTrack.FilePath);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => PlayTrack(nextFile));
        }

        private void Player_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => PlayNextTrack());
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.TogglePlayPause();
            UpdatePlayButtonState();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"PrevButton_Click. CurrentPlaylistIndex: {_currentPlaylistIndex}");

            if (_currentPlaylist.Count == 0) return;

            if (_repeatMode == RepeatMode.One)
            {
                var session = MediaPlayerSingleton.Player.PlaybackSession;
                if (session != null && session.Position.TotalSeconds > 3)
                {
                    session.Position = TimeSpan.Zero;
                    return;
                }
            }

            int prevIndex = _currentPlaylistIndex - 1;
            if (prevIndex < 0)
            {
                if (_repeatMode == RepeatMode.All)
                {
                    prevIndex = _currentPlaylist.Count - 1;
                }
                else
                {
                    return;
                }
            }

            var prevTrack = _currentPlaylist[prevIndex];
            System.Diagnostics.Debug.WriteLine($"Предыдущий трек: {prevTrack.Title}");

            var file = StorageFile.GetFileFromPathAsync(prevTrack.FilePath).AsTask().Result;
            PlayTrack(file);
        }
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist.Count == 0) return;

            int nextIndex = _currentPlaylistIndex + 1;
            if (nextIndex >= _currentPlaylist.Count)
            {
                if (_repeatMode == RepeatMode.All)
                {
                    nextIndex = 0;
                }
                else
                {
                    return;
                }
            }

            var nextTrack = _currentPlaylist[nextIndex];
            var file = StorageFile.GetFileFromPathAsync(nextTrack.FilePath).AsTask().Result;
            PlayTrack(file);
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
                await PlayNextTrack();
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
            switch (_repeatMode)
            {
                case RepeatMode.None:
                    _repeatMode = RepeatMode.One;
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_one_white.png"));
                    break;
                case RepeatMode.One:
                    _repeatMode = RepeatMode.All;
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_all_white.png"));
                    break;
                case RepeatMode.All:
                    _repeatMode = RepeatMode.None;
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_none_white.png"));
                    break;
            }
        }

        private async void TrackInfo_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrack == null) return;

            var dialog = new Windows.UI.Popups.MessageDialog(
                $"Название: {currentTrack.Title}\n" +
                $"Исполнитель: {currentTrack.Artist}\n" +
                $"Альбом: {currentTrack.Album}\n" +
                $"Длительность: {currentTrack.Duration.ToString(@"mm\:ss")}\n" +
                $"Путь: {currentTrack.FilePath}",
                "Информация о треке"
            );

            await dialog.ShowAsync();
        }

        private async void AddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrack == null)
            {
                var dialog = new Windows.UI.Popups.MessageDialog("Нет активного трека");
                await dialog.ShowAsync();
                return;
            }

            // Получаем список плейлистов из MainPage через App
            var playlists = App.CurrentPlaylists;

            if (playlists == null || playlists.Count == 0)
            {
                var dialog = new Windows.UI.Popups.MessageDialog("Нет плейлистов. Создайте первый.");
                await dialog.ShowAsync();
                return;
            }

            // Выбираем плейлист
            var options = playlists.Select(p => p.Name).ToArray();
            var selectedIndex = await ShowPlaylistPicker(options);

            if (selectedIndex >= 0)
            {
                var playlist = playlists[selectedIndex];
                if (!playlist.Tracks.Any(t => t.FilePath == currentTrack.FilePath))
                {
                    playlist.Tracks.Add(currentTrack);
                    await PlaylistStorage.SavePlaylistsAsync(playlists);

                    var dialog = new Windows.UI.Popups.MessageDialog($"Трек добавлен в плейлист \"{playlist.Name}\"");
                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new Windows.UI.Popups.MessageDialog("Трек уже есть в этом плейлисте");
                    await dialog.ShowAsync();
                }
            }
        }

        private async Task<int> ShowPlaylistPicker(string[] options)
        {
            var dialog = new ContentDialog
            {
                Title = "Выберите плейлист",
                PrimaryButtonText = "Добавить",
                SecondaryButtonText = "Отмена"
            };

            var listBox = new ListBox();
            foreach (var opt in options)
                listBox.Items.Add(opt);
            listBox.SelectedIndex = 0;
            dialog.Content = listBox;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && listBox.SelectedItem != null)
                return Array.IndexOf(options, listBox.SelectedItem.ToString());
            return -1;
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

        public void UpdateData(TrackItem track, List<TrackItem> playlist, int playlistIndex, RepeatMode repeatMode)
        {
            if (track == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateData: track is NULL");
                return;
            }

            currentTrack = track;
            _currentPlaylist = playlist ?? new List<TrackItem>();
            _currentPlaylistIndex = playlistIndex;
            _repeatMode = repeatMode;

            if (FullTrackTitle != null)
                FullTrackTitle.Text = track.Title;
            if (FullTrackArtist != null)
                FullTrackArtist.Text = track.Artist;

            _ = LoadAlbumArt(track.FilePath);
            UpdatePlayPauseButton();
            ForceUpdatePosition();
        }

        // Методы
        private void UpdateQueue()
        {
            var items = new List<QueueItem>();
            for (int i = 0; i < _currentPlaylist.Count; i++)
            {
                items.Add(new QueueItem
                {
                    Index = i + 1,
                    Title = _currentPlaylist[i].Title,
                    Artist = _currentPlaylist[i].Artist
                });
            }
            QueueListView.ItemsSource = items;
        }

        private async void QueueListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as QueueItem;
            if (item != null)
            {
                int newIndex = item.Index - 1;
                if (newIndex >= 0 && newIndex < _currentPlaylist.Count)
                {
                    var track = _currentPlaylist[newIndex];
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                    PlayTrack(file);
                }
            }
        }
    }
}