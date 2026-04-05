using Decadence.Models;
using Decadence.Services;
using Singleton;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace Decadence
{
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<TrackItem> _tracks = new ObservableCollection<TrackItem>();
        private ObservableCollection<ArtistItem> _artists = new ObservableCollection<ArtistItem>();
        private ObservableCollection<AlbumItem> _albums = new ObservableCollection<AlbumItem>();

        private bool _isLoading = false;
        private bool _isInitialized = false;

        private DispatcherTimer _positionTimer;
        private int _currentTrackIndex = -1;
        private bool _userIsSeeking = false;
        private bool _wasPlayingBeforeSeek = false;

        private List<TrackItem> _currentPlaylist = new List<TrackItem>();
        private bool _isArtistViewActive = false;
        public MainPage()
        {
            this.InitializeComponent();

            _ = InitializeLibraryAsync();
        }

        private async Task InitializeLibraryAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("=== ИНИЦИАЛИЗАЦИЯ БИБЛИОТЕКИ ===");

                bool hasCache = await MusicCacheService.HasCacheAsync();
                List<CachedTrack> cachedTracks;

                if (hasCache)
                {
                    System.Diagnostics.Debug.WriteLine("📖 Загрузка из кэша...");
                    cachedTracks = await MusicCacheService.LoadCacheAsync();
                    ShowTracksFromCache(cachedTracks);

                    System.Diagnostics.Debug.WriteLine("🔄 Фоновая проверка обновлений...");
                    _ = Task.Run(async () =>
                    {
                        var updatedTracks = await MusicCacheService.QuickCheckAsync(cachedTracks);
                        if (updatedTracks.Count != cachedTracks.Count)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                ShowTracksFromCache(updatedTracks);
                            });
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("🔍 Первый запуск, сканирование...");
                    ShowLoadingIndicator(true);

                    cachedTracks = await MusicCacheService.FullScanAsync();
                    ShowTracksFromCache(cachedTracks);
                    ShowLoadingIndicator(false);
                }

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("=== ИНИЦИАЛИЗАЦИЯ ЗАВЕРШЕНА ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }
        private void ShowLoadingIndicator(bool show)
        {
            if (MainPivot != null)
                MainPivot.IsEnabled = !show;
        }
        private void ShowTracksFromCache(List<CachedTrack> cachedTracks)
        {
            _tracks.Clear();
            _artists.Clear();
            _albums.Clear();

            var artistDict = new Dictionary<string, List<TrackItem>>();
            var albumDict = new Dictionary<string, List<TrackItem>>();

            foreach (var cached in cachedTracks)
            {
                var track = new TrackItem
                {
                    FilePath = cached.FilePath,
                    Title = cached.Title,
                    Artist = cached.Artist,
                    Album = cached.Album,
                    Duration = TimeSpan.Parse(cached.Duration)
                };

                _tracks.Add(track);

                if (!artistDict.ContainsKey(track.Artist))
                    artistDict[track.Artist] = new List<TrackItem>();
                artistDict[track.Artist].Add(track);

                if (!albumDict.ContainsKey(track.Album))
                    albumDict[track.Album] = new List<TrackItem>();
                albumDict[track.Album].Add(track);
            }

            foreach (var kvp in artistDict.OrderBy(k => k.Key))
            {
                _artists.Add(new ArtistItem
                {
                    Name = kvp.Key,
                    FirstTrack = kvp.Value.First(),
                    TrackCount = kvp.Value.Count
                });
            }

            foreach (var kvp in albumDict
                .Where(k => k.Key != "Неизвестный альбом")
                .OrderBy(k => k.Key))
            {
                _albums.Add(new AlbumItem
                {
                    Name = kvp.Key,
                    Artist = kvp.Value.First().Artist,
                    FirstTrack = kvp.Value.First(),
                    TrackCount = kvp.Value.Count
                });
            }

            TracksList.ItemsSource = _tracks;
            //ArtistsList.ItemsSource = _artists;
            //AlbumsList.ItemsSource = _albums;

            System.Diagnostics.Debug.WriteLine($"📊 Показано: {_tracks.Count} треков, {_artists.Count} исполнителей, {_albums.Count} альбомов");
        }

        private async void PlayTrack(StorageFile file)
        {
            if (file == null) return;

            var track = _tracks.FirstOrDefault(t => t.FilePath == file.Path);
            if (track == null) return;

            _currentTrackIndex = _tracks.IndexOf(track);
            track.File = file;

            MediaPlayerSingleton.PlayFile(file);

            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "0:00";
            _userIsSeeking = false;

            var saved = ApplicationData.Current.LocalSettings.Values["SavedVolume"];
            MediaPlayerSingleton.Player.Volume = saved is double v ? v : 1.0;

            FullTrackTitle.Text = track.Title;
            FullTrackArtist.Text = track.Artist;

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
        }
        private void Player_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => PlayNextTrack());
        }
        private void ProgressSlider_ManipulationStarted(object sender, Windows.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e)
        {
            _userIsSeeking = true;
            _wasPlayingBeforeSeek = MediaPlayerSingleton.IsPlaying;
            if (_wasPlayingBeforeSeek)
            {
                MediaPlayerSingleton.Player.Pause();
                UpdatePlayButtonState();
            }
        }

        private void UpdatePlayButtonState()
        {
            // Обновляем кнопку в полноэкранном плеере (Image)
            if (FullPlayPauseIcon is Image fullImage)
            {
                string iconName = MediaPlayerSingleton.IsPlaying ? "pause.png" : "play.png";
                var source = new BitmapImage(new Uri($"ms-appx:///Assets/{iconName}"));
                fullImage.Source = source;
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
        private void PlayNextTrack()
        {
            if (_currentPlaylist.Count == 0 || _currentTrackIndex < 0)
            {
                if (_tracks.Count == 0) return;

                int globalNextIndex = (_currentTrackIndex + 1) % _tracks.Count;
                var globalNextTrack = _tracks[globalNextIndex];

                try
                {
                    var file = StorageFile.GetFileFromPathAsync(globalNextTrack.FilePath).AsTask().Result;
                    PlayTrack(file);
                }
                catch { }
                return;
            }

            int currentIndex = _currentPlaylist.FindIndex(t => t.FilePath == _tracks[_currentTrackIndex]?.FilePath);
            if (currentIndex < 0) currentIndex = 0;

            int playlistNextIndex = (currentIndex + 1) % _currentPlaylist.Count;
            var playlistNextTrack = _currentPlaylist[playlistNextIndex];

            try
            {
                var file = StorageFile.GetFileFromPathAsync(playlistNextTrack.FilePath).AsTask().Result;
                PlayTrack(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.TogglePlayPause();
            UpdatePlayButtonState();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist.Count == 0)
            {
                if (_tracks.Count == 0) return;
                _currentTrackIndex = Math.Max(0, _currentTrackIndex - 1);
                var track = _tracks[_currentTrackIndex];
                try
                {
                    var file = StorageFile.GetFileFromPathAsync(track.FilePath).AsTask().Result;
                    PlayTrack(file);
                }
                catch { }
                return;
            }

            int currentIndex = _currentPlaylist.FindIndex(t => t.FilePath == _tracks[_currentTrackIndex]?.FilePath);
            if (currentIndex < 0) currentIndex = 0;

            int prevIndex = (currentIndex - 1 + _currentPlaylist.Count) % _currentPlaylist.Count;
            var prevTrack = _currentPlaylist[prevIndex];

            try
            {
                var file = StorageFile.GetFileFromPathAsync(prevTrack.FilePath).AsTask().Result;
                PlayTrack(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка PrevButton: {ex.Message}");
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist.Count == 0)
            {
                if (_tracks.Count == 0) return;
                _currentTrackIndex = Math.Min(_tracks.Count - 1, _currentTrackIndex + 1);
                var track = _tracks[_currentTrackIndex];
                try
                {
                    var file = StorageFile.GetFileFromPathAsync(track.FilePath).AsTask().Result;
                    PlayTrack(file);
                }
                catch { }
                return;
            }

            int currentIndex = _currentPlaylist.FindIndex(t => t.FilePath == _tracks[_currentTrackIndex]?.FilePath);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = (currentIndex + 1) % _currentPlaylist.Count;
            var nextTrack = _currentPlaylist[nextIndex];

            try
            {
                var file = StorageFile.GetFileFromPathAsync(nextTrack.FilePath).AsTask().Result;
                PlayTrack(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка NextButton: {ex.Message}");
            }
        }

        private async void ProgressSlider_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            _userIsSeeking = false;
            var session = MediaPlayerSingleton.Player.PlaybackSession;
            if (session == null) return;

            double remainingTime = session.NaturalDuration.TotalSeconds - ProgressSlider.Value;

            if (remainingTime < 2 && ProgressSlider.Value > 0)
            {
                await Task.Run(() => PlayNextTrack());
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


        private void ArtistsButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            System.Diagnostics.Debug.WriteLine("Artists - будет позже");
        }

        private void AlbumsButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            System.Diagnostics.Debug.WriteLine("Albums - будет позже");
        }

        private void PlaylistsButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            System.Diagnostics.Debug.WriteLine("Playlists - будет позже");
        }

        private void GenresButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            System.Diagnostics.Debug.WriteLine("Genres - будет позже");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            SettingsPanel.Visibility = Visibility.Visible;
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            var dialog = new Windows.UI.Popups.MessageDialog("Decadence\nВерсия 0.1\nМузыкальный плеер", "О программе");
            _ = dialog.ShowAsync();
        }

        private void TracksButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Треков в _tracks: {_tracks.Count}");

            if (_tracks.Count > 0)
            {
                TracksList.ItemsSource = _tracks;
                TracksPanel.Visibility = Visibility.Visible;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ _tracks пуст!");
            }
        }

        private void ClosePanel_Click(object sender, RoutedEventArgs e)
        {
            TracksPanel.Visibility = Visibility.Collapsed;
        }

        private async void Track_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                    MediaPlayerSingleton.PlayFile(file);

                    // Закрываем панель после выбора трека
                    ClosePanel_Click(null, null);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
                }
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower().Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                return;
            }

            var results = _tracks.Where(t =>
                (t.Title?.ToLower().Contains(searchText) ?? false) ||
                (t.Artist?.ToLower().Contains(searchText) ?? false) ||
                (t.Album?.ToLower().Contains(searchText) ?? false)
            ).ToList();

            if (results.Any())
            {
                SearchResultsList.ItemsSource = results;
                SearchResultsList.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;

                if (EmptyStatePanel.Children[1] is TextBlock emptyText)
                    emptyText.Text = "Ничего не найдено";
            }
        }

        private async void SearchResult_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                    _currentPlaylist = new List<TrackItem> { track };
                    _isArtistViewActive = false;
                    PlayTrack(file);
                }
                catch
                {
                    _tracks.Remove(track);
                    SearchBox_TextChanged(null, null);
                }
            }
        }
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Когда пользователь кликает на поисковик
            // Можно очистить текст-подсказку или выделить весь текст
            SearchBox.SelectAll(); // Выделить весь текст, если он есть
        }
        private async void RefreshLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var dialog = new Windows.UI.Popups.MessageDialog(
                "Обновление библиотеки пересканирует все музыкальные файлы. Это может занять несколько минут. Продолжить?",
                "Обновление библиотеки");

            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Да") { Id = 0 });
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Нет") { Id = 1 });
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;

            var result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
                await RefreshLibraryAsync();
        }
        private async Task RefreshLibraryAsync()
        {
            try
            {
                _isLoading = true;
                ShowLoadingIndicator(true);

                System.Diagnostics.Debug.WriteLine("🔄 Принудительное обновление библиотеки...");

                var cachedTracks = await MusicCacheService.FullScanAsync();
                ShowTracksFromCache(cachedTracks);

                var completeDialog = new Windows.UI.Popups.MessageDialog(
                    $"Библиотека обновлена. Найдено {cachedTracks.Count} треков.", "Готово");
                _ = completeDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка обновления: {ex.Message}");

                var errorDialog = new Windows.UI.Popups.MessageDialog(
                    $"Ошибка при обновлении: {ex.Message}", "Ошибка");
                _ = errorDialog.ShowAsync();
            }
            finally
            {
                _isLoading = false;
                ShowLoadingIndicator(false);
            }
        }

        private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }
    }
}

