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
        private TrackItem _currentTrack;

        public event EventHandler Clicked;
        private TrackItem currentTrack;
        private DispatcherTimer _positionTimer;
        private bool _userIsSeeking = false;
        private bool _wasPlayingBeforeSeek = false;
        private RepeatMode _repeatMode = RepeatMode.None;

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

            if (e.Parameter is FullPlayerNavigationData data)
            {
                if (data.Track != null)
                {
                    // Используем переданные данные
                    currentTrack = data.Track;
                    _currentPlaylist = data.Playlist ?? new List<TrackItem>();
                    _currentPlaylistIndex = data.PlaylistIndex;
                    _repeatMode = data.CurrentRepeatMode;

                    FullTrackTitle.Text = currentTrack.Title;
                    FullTrackArtist.Text = currentTrack.Artist;

                    // НЕ ЗАПУСКАЕМ ТРЕК ЗАНОВО, а просто обновляем UI
                    await LoadAlbumArt(currentTrack.FilePath);
                    UpdatePlayPauseButton();

                    // Обновляем позицию слайдера
                    var session = MediaPlayerSingleton.Player?.PlaybackSession;
                    if (session != null && session.NaturalDuration > TimeSpan.Zero)
                    {
                        ProgressSlider.Maximum = session.NaturalDuration.TotalSeconds;
                        ProgressSlider.Value = session.Position.TotalSeconds;
                        CurrentTimeText.Text = FormatTime(session.Position);
                        TotalTimeText.Text = FormatTime(session.NaturalDuration);
                    }
                }
                else
                {
                    FullTrackTitle.Text = "Нет трека";
                    FullTrackArtist.Text = "Выберите трек в библиотеке";
                }
            }
            else
            {
                FullTrackTitle.Text = "Нет трека";
                FullTrackArtist.Text = "Выберите трек в библиотеке";
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
            _positionTimer?.Stop();
            MediaPlayerSingleton.Player.MediaEnded -= Player_MediaEnded;
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

            _currentTrack = track;
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

            if (_positionTimer == null)
                StartPositionTimer();
            else
                _positionTimer.Start();
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

        private void StartPositionTimer()
        {
            _positionTimer?.Stop();
            _positionTimer = new DispatcherTimer();
            _positionTimer.Interval = TimeSpan.FromMilliseconds(500);
            _positionTimer.Tick += (s, e) =>
            {
                var session = MediaPlayerSingleton.Player.PlaybackSession;
                if (session?.NaturalDuration > TimeSpan.Zero)
                {
                    ProgressSlider.Maximum = session.NaturalDuration.TotalSeconds;
                    TotalTimeText.Text = FormatTime(session.NaturalDuration);

                    if (!_userIsSeeking)
                    {
                        ProgressSlider.Value = session.Position.TotalSeconds;
                        CurrentTimeText.Text = FormatTime(session.Position);
                    }
                }
            };
            _positionTimer.Start();
        }

        private async Task PlayNextTrack()
        {
            System.Diagnostics.Debug.WriteLine($"PlayNextTrack вызван. CurrentPlaylistIndex: {_currentPlaylistIndex}, Playlist.Count: {_currentPlaylist.Count}");

            if (_currentPlaylist.Count == 0) return;

            if (_repeatMode == RepeatMode.One)
            {
                if (_currentTrack != null)
                {
                    var trackFile = await StorageFile.GetFileFromPathAsync(_currentTrack.FilePath);
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
            if (_currentTrack == null) return;

            var dialog = new Windows.UI.Popups.MessageDialog(
                $"Название: {_currentTrack.Title}\n" +
                $"Исполнитель: {_currentTrack.Artist}\n" +
                $"Альбом: {_currentTrack.Album}\n" +
                $"Длительность: {_currentTrack.Duration.ToString(@"mm\:ss")}\n" +
                $"Путь: {_currentTrack.FilePath}",
                "Информация о треке"
            );

            await dialog.ShowAsync();
        }

        private async void AddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Windows.UI.Popups.MessageDialog(
                "Пардон! \nЭта функция будет добавлена в ближайшее время",
                "Плейлисты"
            );
            await dialog.ShowAsync();
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


    }
}