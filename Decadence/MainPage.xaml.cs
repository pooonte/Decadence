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

        private DispatcherTimer _miniPlayerTimer;

        private List<TrackItem> _currentPlaylist = new List<TrackItem>();
        private int _currentPlaylistIndex = -1;

        private RepeatMode _repeatMode = RepeatMode.None;

        private List<string> _phrases = new List<string>();
        private Random _random = new Random();
        public MainPage()
        {
            this.InitializeComponent();

            _ = InitializeLibraryAsync();

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            Window.Current.CoreWindow.KeyDown += OnKeyDown;
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

            if (TracksPanelControl.IsVisible)
            {
                TracksPanelControl.Hide();
                anyPanelClosed = true;
            }
            if (ArtistsPanelControl.IsVisible)
            {
                ArtistsPanelControl.Hide();
                anyPanelClosed = true;
            }
            if (AlbumsPanelControl.IsVisible)
            {
                AlbumsPanelControl.Hide();
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
            UpdateStatistics();
            UpdateTrackCount();

            System.Diagnostics.Debug.WriteLine($"📊 Показано: {_tracks.Count} треков, {_artists.Count} исполнителей, {_albums.Count} альбомов");
        }
        private void UpdateStatistics()
        {
            // Обновляем UI в главном потоке
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TrackCountNumber.Text = _tracks.Count.ToString();
            });
        }
        private async void PlayTrack(StorageFile file)
        {
            System.Diagnostics.Debug.WriteLine("▶️ PlayTrack ВЫЗВАН");

            if (file == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ file == null");
                return;
            }

            var track = _currentPlaylist.FirstOrDefault(t => t.FilePath == file.Path);
            if (track == null)
            {
                System.Diagnostics.Debug.WriteLine("🔍 Трек не найден в _currentPlaylist, ищу в _tracks");
                track = _tracks.FirstOrDefault(t => t.FilePath == file.Path);
                if (track == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Трек не найден в _tracks");
                    return;
                }

                _currentPlaylist = _tracks.ToList();
                _currentPlaylistIndex = _tracks.IndexOf(track);
                System.Diagnostics.Debug.WriteLine($"✅ Плейлист создан, индекс: {_currentPlaylistIndex}");
            }
            else
            {
                _currentPlaylistIndex = _currentPlaylist.IndexOf(track);
            }

            System.Diagnostics.Debug.WriteLine($"🎵 Трек: {track.Title}");
            currentTrack = track;

            App.CurrentTrack = currentTrack;
            App.CurrentPlaylist = _currentPlaylist;
            App.CurrentPlaylistIndex = _currentPlaylistIndex;
            App.CurrentRepeatMode = _repeatMode;

            System.Diagnostics.Debug.WriteLine(" Запуск MediaPlayerSingleton.PlayFile");
            MediaPlayerSingleton.PlayFile(file);

            var saved = ApplicationData.Current.LocalSettings.Values["SavedVolume"];
            MediaPlayerSingleton.Player.Volume = saved is double v ? v : 1.0;
            System.Diagnostics.Debug.WriteLine("✅ PlayTrack завершён");
        }
        private void PlaylistsButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            var dialog = new Windows.UI.Popups.MessageDialog("Придется подождать чуть чуть!\nПока что во мне не так много функций. \nИзвините! :(", "Упс!");
            _ = dialog.ShowAsync();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            var dialog = new Windows.UI.Popups.MessageDialog("Decadence\nВерсия 0.1\nМузыкальный плеер", "О программе");
            _ = dialog.ShowAsync();
        }
        private void ArtistsButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Артистов в _artists: {_artists.Count}");

            if (_artists.Count > 0)
            {
                ArtistsPanelControl.SetArtists(_artists);
                ArtistsPanelControl.Show();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ _artists пуст!");
            }
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
        // Обработчик клика по треку
        private async void TracksPanelControl_TrackClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                _currentPlaylist = _tracks.ToList();
                _currentPlaylistIndex = _tracks.IndexOf(track);

                // Воспроизводим трек
                var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                PlayTrack(file);  // ← ДОБАВЬ ЭТУ СТРОКУ

                var navigationData = new FullPlayerNavigationData
                {
                    Track = track,
                    Playlist = _currentPlaylist,
                    PlaylistIndex = _currentPlaylistIndex,
                    CurrentRepeatMode = _repeatMode
                };

                TracksPanelControl.Hide();
                Frame.Navigate(typeof(PlayerMenu), navigationData);
            }
        }
        private void TracksButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tracks.Count > 0)
            {
                TracksPanelControl.SetTracks(_tracks);
                TracksPanelControl.Show();
            }
        }
        // Обработчик кнопки назад
        private void TracksPanelControl_BackClicked(object sender, EventArgs e)
        {
            TracksPanelControl.Hide();
        }
        // Обработчик клика по артисту
        private void ArtistsPanelControl_ArtistClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ArtistItem artist)
            {
                var tracks = _tracks.Where(t => t.Artist == artist.Name).ToList();
                ArtistsPanelControl.SetTracks(tracks, artist.Name);
            }
        }

        // Обработчик клика по треку
        private async void ArtistsPanelControl_TrackClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                _currentPlaylist = _tracks.Where(t => t.Artist == track.Artist).ToList();
                _currentPlaylistIndex = _currentPlaylist.IndexOf(track);

                var navigationData = new FullPlayerNavigationData
                {
                    Track = track,
                    Playlist = _currentPlaylist,
                    PlaylistIndex = _currentPlaylistIndex,
                    CurrentRepeatMode = _repeatMode
                };

                Frame.Navigate(typeof(PlayerMenu), navigationData);
                ArtistsPanelControl.Hide();
            }
        }

        // Обработчик кнопки "Назад"
        private void ArtistsPanelControl_BackClicked(object sender, EventArgs e)
        {
            ArtistsPanelControl.Hide();
        }

        // Открыть панель альбомов
        private void AlbumsButton_Click(object sender, RoutedEventArgs e)
        {
            AlbumsPanelControl.SetAlbums(_albums);
            AlbumsPanelControl.Show();
        }

        // Клик по альбому
        private void AlbumsPanelControl_AlbumClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AlbumItem album)
            {
                var tracks = _tracks.Where(t => t.Album == album.Name).ToList();
                AlbumsPanelControl.SetTracks(tracks, album.Name);
            }
        }

        // Клик по треку
        private async void AlbumsPanelControl_TrackClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                _currentPlaylist = _tracks.Where(t => t.Album == track.Album).ToList();
                _currentPlaylistIndex = _currentPlaylist.IndexOf(track);

                var navigationData = new FullPlayerNavigationData
                {
                    Track = track,
                    Playlist = _currentPlaylist,
                    PlaylistIndex = _currentPlaylistIndex,
                    CurrentRepeatMode = _repeatMode
                };

                Frame.Navigate(typeof(PlayerMenu), navigationData);
                AlbumsPanelControl.Hide();
            }
        }
        private void AlbumsPanelControl_BackClicked(object sender, EventArgs e)
        {
            AlbumsPanelControl.Hide();
        }

        // Обновление счетчика (вызывай после загрузки треков)
        private void UpdateTrackCount()
        {
            TrackCountNumber.Text = _tracks.Count.ToString();
        }

        // Переход в PlayerMenu
        private void StatsButton_Click(object sender, RoutedEventArgs e)
        {
            var navData = new FullPlayerNavigationData
            {
                Track = App.CurrentTrack,
                Playlist = App.CurrentPlaylist,
                PlaylistIndex = App.CurrentPlaylistIndex,
                CurrentRepeatMode = App.CurrentRepeatMode
            };

            Frame.Navigate(typeof(PlayerMenu), navData);
        }
    }
}

