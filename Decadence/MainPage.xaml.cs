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
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<TrackItem> _tracks = new ObservableCollection<TrackItem>();
        private ObservableCollection<ArtistItem> _artists = new ObservableCollection<ArtistItem>();
        private ObservableCollection<AlbumItem> _albums = new ObservableCollection<AlbumItem>();

        private bool _isLoading = false;
        private bool _isInitialized = false;

        private TrackItem currentTrack;

        private DispatcherTimer _positionTimer;
        private List<TrackItem> _currentPlaylist = new List<TrackItem>();
        private int _currentPlaylistIndex = -1;
        private bool _userIsSeeking = false;
        private bool _wasPlayingBeforeSeek = false;
        private bool _isArtistViewActive = false;

        private enum RepeatMode { None, One, All }
        private RepeatMode _repeatMode = RepeatMode.None;

        private List<string> _phrases = new List<string>();
        private Random _random = new Random();

        private BitmapImage _playIcon;
        private BitmapImage _pauseIcon;
        public MainPage()
        {
            this.InitializeComponent();

            _ = InitializeLibraryAsync();

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            Window.Current.CoreWindow.KeyDown += OnKeyDown;
            MediaPlayerSingleton.Player.MediaEnded += Player_MediaEnded;

            // 🔹 Загружаем иконки ОДИН раз при старте
            _playIcon = new BitmapImage(new Uri("ms-appx:///Assets/play.png"));
            _pauseIcon = new BitmapImage(new Uri("ms-appx:///Assets/pause.png"));

            // Устанавливаем начальную иконку
            if (FullPlayPauseIcon != null)
                FullPlayPauseIcon.Source = _playIcon;
        }
        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (CloseAnyOpenPanel())
            {
                e.Handled = true; // Закрыли панель, приложение не закрываем
            }
            else
            {
                e.Handled = false; // Панелей нет, можно закрыть приложение
            }
        }

        private void OnKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.Escape)
            {
                CloseAnyOpenPanel();
                args.Handled = true;
            }
        }
        private bool CloseAnyOpenPanel()
        {
            bool anyPanelClosed = false;

            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                anyPanelClosed = true;
            }

            if (TracksPanel.Visibility == Visibility.Visible)
            {
                TracksPanel.Visibility = Visibility.Collapsed;
                anyPanelClosed = true;
            }

            if (ArtistsPanel.Visibility == Visibility.Visible)
            {
                ArtistsPanel.Visibility = Visibility.Collapsed;
                anyPanelClosed = true;
            }
            //нужно добавлять панели сюда
            return anyPanelClosed;
        }
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            Window.Current.CoreWindow.KeyDown -= OnKeyDown;
            base.OnNavigatingFrom(e);
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
            ArtistsList.ItemsSource = _artists;
            //AlbumsList.ItemsSource = _albums;

            System.Diagnostics.Debug.WriteLine($"📊 Показано: {_tracks.Count} треков, {_artists.Count} исполнителей, {_albums.Count} альбомов");
        }

        private async void PlayTrack(StorageFile file)
        {
            if (file == null) return;

            var track = _currentPlaylist.FirstOrDefault(t => t.FilePath == file.Path);
            if (track == null)
            {
                track = _tracks.FirstOrDefault(t => t.FilePath == file.Path);
                if (track == null) return;

                _currentPlaylist = _tracks.ToList();
                _currentPlaylistIndex = _tracks.IndexOf(track);
            }
            else
            {
                _currentPlaylistIndex = _currentPlaylist.IndexOf(track);
            }

            currentTrack = track;
            MediaPlayerSingleton.PlayFile(file);

            // ===== ОБНОВЛЯЕМ UI =====
            FullTrackTitle.Text = track.Title;
            FullTrackArtist.Text = track.Artist;

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
            // 🔹 Просто меняем ссылку на УЖЕ загруженную иконку
            if (FullPlayPauseIcon != null)
            {
                FullPlayPauseIcon.Source = MediaPlayerSingleton.IsPlaying
                    ? _pauseIcon   // Используем предзагруженную
                    : _playIcon;   // Используем предзагруженную
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
            if (_currentPlaylist.Count == 0) return;

            if (_repeatMode == RepeatMode.One)
            {
                var trackFile = await StorageFile.GetFileFromPathAsync(currentTrack.FilePath);
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => PlayTrack(trackFile));
                return;
            }

            int nextIndex = _currentPlaylistIndex + 1;

            if (nextIndex >= _currentPlaylist.Count)
            {
                if (_repeatMode == RepeatMode.None)
                {
                    MediaPlayerSingleton.Player.Pause();
                    return;
                }
                else if (_repeatMode == RepeatMode.All)
                {
                    nextIndex = 0;
                }
            }

            var nextTrack = _currentPlaylist[nextIndex];
            var nextFile = await StorageFile.GetFileFromPathAsync(nextTrack.FilePath);
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => PlayTrack(nextFile));
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.TogglePlayPause();
            UpdatePlayButtonState();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
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

        private async void ProgressSlider_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
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


        private void ArtistsButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            ArtistsPanel.Visibility = Visibility.Visible;
        }

        private void AlbumsButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            AlbumsPanel.Visibility = Visibility.Visible;
        }

        private void PlaylistsButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            System.Diagnostics.Debug.WriteLine("Playlists - будет позже");
        }

        private void GenresButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Genres - будет позже");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
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

        // Из TracksPanel
        private async void Track_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                // Создаем плейлист из всех треков
                _currentPlaylist = _tracks.ToList();
                _currentPlaylistIndex = _tracks.IndexOf(track);

                var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                PlayTrack(file);

                MainPivot.SelectedIndex = 0;
                TracksPanel.Visibility = Visibility.Collapsed;
            }
        }

        // Из Search
        private async void SearchResult_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                _currentPlaylist = _tracks.ToList();
                _currentPlaylistIndex = _tracks.IndexOf(track);

                var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                PlayTrack(file);

                MainPivot.SelectedIndex = 0;
                SearchPanel.Visibility = Visibility.Collapsed;
            }
        }

        // Из Artist
        private async void ArtistSong_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                _currentPlaylistIndex = _currentPlaylist.IndexOf(track);

                var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                PlayTrack(file);

                MainPivot.SelectedIndex = 0;
                ArtistsPanel.Visibility = Visibility.Collapsed;
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower().Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Нет текста - скрываем панель
                SearchPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Есть текст - показываем панель
                SearchPanel.Visibility = Visibility.Visible;

                // Ищем результаты
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
                }
            }
        }

        // При получении фокуса - показываем панель (если есть текст)
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Открываем панель при клике
            SearchPanel.Visibility = Visibility.Visible;

            // Если текст есть, показываем результаты
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                string searchText = SearchBox.Text.ToLower().Trim();
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
                }
            }
            else
            {
                // Текст пустой - показываем пустое состояние
                SearchResultsList.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
            }

            SearchBox.SelectAll();
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

        private void Artist_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ArtistItem artist)
            {
                _currentPlaylist = _tracks
                    .Where(t => t.Artist == artist.Name)
                    .OrderBy(t => t.Album)
                    .ThenBy(t => t.Title)
                    .ToList();

                _currentPlaylistIndex = 0;

                var artistTracks = _tracks
                    .Where(t => t.Artist == artist.Name)
                    .OrderBy(t => t.Album)
                    .ThenBy(t => t.Title)
                    .ToList();

                int trackNumber = 1;
                foreach (var track in artistTracks)
                {
                    track.TrackNumber = trackNumber++;
                }

                _currentPlaylist = artistTracks;
                _isArtistViewActive = true;

                SelectedArtistName.Text = artist.Name;
                ArtistSongsList.ItemsSource = artistTracks;

                ArtistsList.Visibility = Visibility.Collapsed;
                ArtistSongsPanel.Visibility = Visibility.Visible;
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

        // Добавить в плейлист (пока заглушка)
        private async void AddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Windows.UI.Popups.MessageDialog(
                "Эта функция будет доступна в следующей версии",
                "Добавление в плейлист"
            );
            await dialog.ShowAsync();
        }
        private void BackToArtistsButton_Click(object sender, RoutedEventArgs e)
        {
            ArtistsList.Visibility = Visibility.Visible;
            ArtistSongsPanel.Visibility = Visibility.Collapsed;

            _currentPlaylist = _tracks.ToList();
            _isArtistViewActive = false;
        }
        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            // Переключение режимов: None -> One -> All -> None
            switch (_repeatMode)
            {
                case RepeatMode.None:
                    _repeatMode = RepeatMode.One;
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_one.png"));
                    break;
                case RepeatMode.One:
                    _repeatMode = RepeatMode.All;
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_all.png"));
                    break;
                case RepeatMode.All:
                    _repeatMode = RepeatMode.None;
                    RepeatButtonImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/repeat_none.png"));
                    break;
            }
            System.Diagnostics.Debug.WriteLine($"RepeatMode изменен на: {_repeatMode}");
        }

        private void Album_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AlbumItem album)
            {
                var albumTracks = _tracks
                    .Where(t => t.Album == album.Name)
                    .OrderBy(t => t.Title)
                    .ToList();

                int trackNumber = 1;
                foreach (var track in albumTracks)
                {
                    track.TrackNumber = trackNumber++;
                }

                _currentPlaylist = albumTracks;
                _currentPlaylistIndex = 0;  // <-- добавь эту строку

                //SelectedAlbumName.Text = album.Name;
                AlbumSongsList.ItemsSource = albumTracks;

                AlbumsList.Visibility = Visibility.Collapsed;
                AlbumSongsPanel.Visibility = Visibility.Visible;
            }
        }

        private async void AlbumSong_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);

                    int index = _currentPlaylist.IndexOf(track);
                    if (index >= 0)
                    {
                        _currentPlaylistIndex = index;  // <-- замени _currentTrackIndex на _currentPlaylistIndex
                        PlayTrack(file);
                    }

                    // Переключиться на FullPlayer и закрыть панель
                    MainPivot.SelectedIndex = 0;
                    AlbumsPanel.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
                }
            }
        }

        private void BackToAlbumsButton_Click(object sender, RoutedEventArgs e)
        {
            AlbumsList.Visibility = Visibility.Visible;
            AlbumSongsPanel.Visibility = Visibility.Collapsed;

            _currentPlaylist = _tracks.ToList();
            _currentPlaylistIndex = -1;  // <-- добавь
        }
    }
}

