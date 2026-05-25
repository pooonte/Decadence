using Decadence.Models;
using Decadence.Services;
using Singleton;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Decadence
{
    public sealed partial class PlayerMenu : Page
    {
        private List<TrackItem> _currentPlaylist = new List<TrackItem>();
        private int _currentPlaylistIndex = -1;
        private readonly List<QueueItem> _queueCache = new List<QueueItem>();

        public event EventHandler Clicked;
        private TrackItem currentTrack;
        private bool _userIsSeeking = false;
        private bool _wasPlayingBeforeSeek = false;
        private RepeatMode _repeatMode = RepeatMode.None;
        private DispatcherTimer _globalTimer;
        private bool _isActive = false;
        private BitmapImage _playIcon;
        private BitmapImage _pauseIcon;

        private Random _random = new Random();

        private BitmapImage _repeatNoneIcon;
        private BitmapImage _repeatOneIcon;
        private BitmapImage _repeatAllIcon;
        private BitmapImage _currentAlbumArt;
        private Random _shuffleRandom = new Random();
        private bool _isShuffleEnabled = false;
        private List<TrackItem> _originalPlaylist = new List<TrackItem>();
        public PlayerMenu()
        {
            this.InitializeComponent();
            _playIcon = new BitmapImage(new Uri("ms-appx:///Assets/play_white.png"));
            _pauseIcon = new BitmapImage(new Uri("ms-appx:///Assets/pause_white.png"));

            MediaPlayerSingleton.Player.MediaEnded += Player_MediaEnded;

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
                UpdatePlayButtonState();
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
                UpdatePlayButtonState();
            }
            else
            {
                FullTrackTitle.Text = "Нет трека";
                FullTrackArtist.Text = "Выберите трек в библиотеке";
            }

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
                // 🔹 Освобождаем старый BitmapImage
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
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _isActive = false;
            if (_globalTimer != null)
            {
                _globalTimer.Stop();
                _globalTimer.Tick -= GlobalTimer_Tick;
                _globalTimer = null;
            }
            MediaPlayerSingleton.Player.MediaEnded -= Player_MediaEnded;

            this.DataContext = null;
            if (QueueListView != null) QueueListView.ItemsSource = null;
            if (FullAlbumArt != null)
            {
                FullAlbumArt.Source = null;
            }
            _currentAlbumArt = null;

            base.OnNavigatingFrom(e);
        }
        private void PlayerMenu_Unloaded(object sender, RoutedEventArgs e)
        {
            // Финальная зачистка UI-элементов
            ProgressSlider.Value = 0;
            FullTrackTitle.Text = string.Empty;
            FullTrackArtist.Text = string.Empty;
        }

        private async void PlayTrack(StorageFile file)
        {
            if (file == null) return;

            // 🔹 Очищаем старую обложку перед загрузкой новой
            FullAlbumArt.Source = null;

            // 🔹 Находим трек ПО ФАЙЛУ (не по индексу!)
            var track = _currentPlaylist.FirstOrDefault(t => t.FilePath == file.Path);
            if (track == null) return;

            // 🔹 Обновляем индекс ПОСЛЕ того, как нашли трек
            _currentPlaylistIndex = _currentPlaylist.IndexOf(track);

            currentTrack = track;
            MediaPlayerSingleton.PlayFile(file);

            // 🔹 Обновляем UI
            FullTrackTitle.Text = track.Title;
            FullTrackArtist.Text = track.Artist;

            App.CurrentTrack = currentTrack;
            App.CurrentPlaylist = _currentPlaylist;
            App.CurrentPlaylistIndex = _currentPlaylistIndex;
            App.CurrentRepeatMode = _repeatMode;

            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "0:00";
            _userIsSeeking = false;

            var saved = ApplicationData.Current.LocalSettings.Values["SavedVolume"];
            MediaPlayerSingleton.Player.Volume = saved is double v ? v : 1.0;

            await LoadAlbumArt(file.Path);
            UpdatePlayButtonState();
            GC.Collect(0, GCCollectionMode.Optimized);
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
                    RepeatButtonImage.Source = _repeatNoneIcon;
                    break;
                case RepeatMode.One:
                    RepeatButtonImage.Source = _repeatOneIcon;
                    break;
                case RepeatMode.All:
                    RepeatButtonImage.Source = _repeatAllIcon;
                    break;
            }
        }

        private void UpdateTimeDisplay(TimeSpan position, TimeSpan duration)
        {
            // Обновляй Text напрямую, без создания промежуточных строк
            CurrentTimeText.Text = $"{(int)position.TotalMinutes}:{position.Seconds:D2}";
            TotalTimeText.Text = $"{(int)duration.TotalMinutes}:{duration.Seconds:D2}";
        }

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

        private async void PrevButton_Click(object sender, RoutedEventArgs e)
        {
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
                prevIndex = (_repeatMode == RepeatMode.All) ? _currentPlaylist.Count - 1 : -1;
                if (prevIndex < 0) return;
            }

            var prevTrack = _currentPlaylist[prevIndex];
            var file = await StorageFile.GetFileFromPathAsync(prevTrack.FilePath); // 🔹 await вместо .Result
            PlayTrack(file);
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist.Count == 0) return;

            int nextIndex = _currentPlaylistIndex + 1;
            if (nextIndex >= _currentPlaylist.Count)
            {
                nextIndex = (_repeatMode == RepeatMode.All) ? 0 : -1;
                if (nextIndex < 0) return;
            }

            var nextTrack = _currentPlaylist[nextIndex];
            var file = await StorageFile.GetFileFromPathAsync(nextTrack.FilePath); // 🔹 await вместо .Result
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
                if (!playlist.Tracks.Any(t => t.FilePath == currentTrack.FilePath))
                {
                    playlist.Tracks.Add(currentTrack);
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
            UpdatePlayButtonState();
            ForceUpdatePosition();
        }

        // Методы
        private void UpdateQueue()
        {
            _queueCache.Clear();
            for (int i = 0; i < _currentPlaylist.Count; i++)
            {
                _queueCache.Add(new QueueItem
                {
                    Index = i + 1,
                    Title = _currentPlaylist[i].Title,
                    Artist = _currentPlaylist[i].Artist
                });
            }

            // 🔹 Разрываем старую привязку перед назначением новой
            QueueListView.ItemsSource = null;
            QueueListView.ItemsSource = _queueCache;
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
        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            _isShuffleEnabled = !_isShuffleEnabled;

            var button = sender as Button;
            var textBlock = button?.Content as TextBlock;

            if (_isShuffleEnabled)
            {
                _originalPlaylist = new List<TrackItem>(_currentPlaylist);
                var shuffled = _currentPlaylist.OrderBy(x => _shuffleRandom.Next()).ToList();
                _currentPlaylist = shuffled;
                _currentPlaylistIndex = 0;
                UpdateQueue();

                if (textBlock != null) textBlock.Text = "Перемешать ✓";
            }
            else
            {
                if (_originalPlaylist.Count > 0)
                {
                    _currentPlaylist = _originalPlaylist;
                    _currentPlaylistIndex = 0;
                    UpdateQueue();
                }

                if (textBlock != null) textBlock.Text = "Перемешать";
            }
        }
    }
}