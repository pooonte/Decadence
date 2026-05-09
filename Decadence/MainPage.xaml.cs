using Decadence.Models;
using Decadence.Services;
using Singleton;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using Microsoft.Graphics.Canvas.UI;

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

        private List<CanvasGeometry> _triangleGeometries = new List<CanvasGeometry>();
        private List<Vector2> _points = new List<Vector2>();
        private List<int> _indices = new List<int>();
        private Random _rnd = new Random();
        private bool _geometryReady = false;
        public MainPage()
        {
            this.InitializeComponent();

            _ = InitializeLibraryAsync();

            _ = LoadPlaylists();

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            Window.Current.CoreWindow.KeyDown += OnKeyDown;

            PlaylistsPanelControl.CreatePlaylist += (s, name) => CreatePlaylist(name);

            TracksPanelControl.AddToPlaylistRequested += TracksPanelControl_AddToPlaylistRequested;

            PlaylistsPanelControl.RemoveTrackRequested += PlaylistsPanelControl_RemoveTrackRequested;

            App.PlaylistsUpdated += OnPlaylistsUpdated;
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
        public void RefreshPlaylistsUI()
        {
            // Вызываем метод контрола, а не напрямую
            PlaylistsPanelControl.SetPlaylists(_playlists);
        }
        private void OnPlaylistsUpdated()
        {
            PlaylistsPanelControl.SetPlaylists(_playlists);
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
            if (App.PlayerMenuInstance != null)
            {
                // Обновляем данные в существующем экземпляре
                App.PlayerMenuInstance.UpdateData(App.CurrentTrack, App.CurrentPlaylist, App.CurrentPlaylistIndex, App.CurrentRepeatMode);
                Frame.Navigate(typeof(PlayerMenu));
            }
            else
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

        private void PlaylistsButton_Click(object sender, RoutedEventArgs e)
        {
            PlaylistsPanelControl.SetPlaylists(_playlists);
            PlaylistsPanelControl.Show();
        }

        private async void PlaylistsPanelControl_PlaylistSelected(object sender, Playlist playlist)
        {
            // Воспроизвести плейлист
            _currentPlaylist = playlist.Tracks.ToList();
            _currentPlaylistIndex = 0;
            if (_currentPlaylist.Count > 0)
            {
                var track = _currentPlaylist[0];
                var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                PlayTrack(file);
            }
        }

        private void PlaylistsPanelControl_PlaylistEditRequested(object sender, Playlist playlist)
        {
            // Меню редактирования (пока заглушка)
            var dialog = new Windows.UI.Popups.MessageDialog(
                $"Плейлист: {playlist.Name}\nТреков: {playlist.TrackCount}",
                "Редактирование");
            _ = dialog.ShowAsync();
        }


        private List<Playlist> _playlists = new List<Playlist>();

        // Загрузка при старте
        private async Task LoadPlaylists()
        {
            _playlists = await PlaylistStorage.LoadPlaylistsAsync();
            App.CurrentPlaylists = _playlists;
            System.Diagnostics.Debug.WriteLine($"Загружено плейлистов: {_playlists.Count}");
        }

        // Добавление трека в плейлист
        private async void AddCurrentTrackToPlaylist(Playlist playlist)
        {
            if (currentTrack == null) return;

            if (!playlist.Tracks.Any(t => t.FilePath == currentTrack.FilePath))
            {
                playlist.Tracks.Add(currentTrack);
                await PlaylistStorage.SavePlaylistsAsync(_playlists);
            }
        }

        // Удаление трека из плейлиста
        private async void RemoveTrackFromPlaylist(Playlist playlist, TrackItem track)
        {
            playlist.Tracks.Remove(track);
            await PlaylistStorage.SavePlaylistsAsync(_playlists);
        }

        // Удаление плейлиста
        private async void DeletePlaylist(Playlist playlist)
        {
            _playlists.Remove(playlist);
            await PlaylistStorage.SavePlaylistsAsync(_playlists);
            PlaylistsPanelControl.SetPlaylists(_playlists);
        }

        private async void TracksPanelControl_AddToPlaylistRequested(object sender, TrackItem track)
        {
            if (_playlists.Count == 0)
            {
                var dialog = new Windows.UI.Popups.MessageDialog("Нет плейлистов. Создайте первый.");
                await dialog.ShowAsync();
                return;
            }

            var options = _playlists.Select(p => p.Name).ToArray();
            var choice = await ShowPlaylistPicker(options);
            if (choice >= 0)
            {
                var playlist = _playlists[choice];
                if (!playlist.Tracks.Any(t => t.FilePath == track.FilePath))
                {
                    playlist.Tracks.Add(track);
                    await PlaylistStorage.SavePlaylistsAsync(_playlists);
                }
            }
        }

        private async Task<int> ShowPlaylistPicker(string[] options)
        {
            var dialog = new ContentDialog
            {
                Title = "Добавить в плейлист",
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

        private async void PlaylistsPanelControl_RemoveTrackRequested(object sender, PlaylistTrackEventArgs e)
        {
            e.Playlist.Tracks.Remove(e.Track);
            await PlaylistStorage.SavePlaylistsAsync(_playlists);
            PlaylistsPanelControl.SetPlaylists(_playlists);
        }

        private async void CreatePlaylist(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            _playlists.Add(new Playlist { Name = name, Tracks = new List<TrackItem>() });
            await PlaylistStorage.SavePlaylistsAsync(_playlists);
            App.CurrentPlaylists = _playlists;
            PlaylistsPanelControl.SetPlaylists(_playlists);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim();

            if (string.IsNullOrEmpty(query))
            {
                // Показываем кнопки, скрываем результаты
                MainButtonsPanel.Visibility = Visibility.Visible;
                SearchResults.Visibility = Visibility.Collapsed;
                return;
            }

            // Ищем
            var results = _tracks.Where(t =>
                t.Title.ToLower().Contains(query.ToLower()) ||
                t.Artist.ToLower().Contains(query.ToLower()) ||
                t.Album.ToLower().Contains(query.ToLower())
            ).ToList();

            // Скрываем кнопки, показываем результаты
            MainButtonsPanel.Visibility = Visibility.Collapsed;
            SearchResults.Visibility = Visibility.Visible;
            SearchResults.ItemsSource = results;
        }
        private void ShowMainButtons()
        {
            var stack = SearchResults.Parent as StackPanel;
            if (stack != null && stack.Children.Count > 1)
            {
                (stack.Children[1] as StackPanel).Visibility = Visibility.Visible;
            }
        }

        private void HideMainButtons()
        {
            var stack = SearchResults.Parent as StackPanel;
            if (stack != null && stack.Children.Count > 1)
            {
                (stack.Children[1] as StackPanel).Visibility = Visibility.Collapsed;
            }
        }
        private async void SearchResult_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                _currentPlaylist = _tracks.ToList();
                _currentPlaylistIndex = _tracks.IndexOf(track);

                var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                PlayTrack(file);

                var navigationData = new FullPlayerNavigationData
                {
                    Track = track,
                    Playlist = _currentPlaylist,
                    PlaylistIndex = _currentPlaylistIndex,
                    CurrentRepeatMode = _repeatMode
                };
                Frame.Navigate(typeof(PlayerMenu), navigationData);

                // Очищаем поиск
                SearchBox.Text = "";
                SearchResults.Visibility = Visibility.Collapsed;
                MainButtonsPanel.Visibility = Visibility.Visible;
            }
        }

        private void LowPolyCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            args.TrackAsyncAction(GenerateGeometryAsync((float)sender.ActualWidth, (float)sender.ActualHeight).AsAsyncAction());
        }

        private async System.Threading.Tasks.Task GenerateGeometryAsync(float width, float height)
        {
            _rnd = new Random(Guid.NewGuid().GetHashCode());
            // Очищаем старую геометрию
            foreach (var geo in _triangleGeometries)
            {
                geo.Dispose();
            }
            _triangleGeometries.Clear();
            _points.Clear();
            _indices.Clear();

            if (width < 10 || height < 10) return;

            float cellSize = 80f;
            float jitter = 70f;

            int cols = (int)(width / cellSize) + 2;
            int rows = (int)(height / cellSize) + 2;

            // Создаем точки
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    float px = (x * cellSize) + (float)(_rnd.NextDouble() * jitter * 2 - jitter);
                    float py = (y * cellSize) + (float)(_rnd.NextDouble() * jitter * 2 - jitter);
                    _points.Add(new Vector2(px, py));
                }
            }

            // Соединяем в индексы
            for (int y = 0; y < rows - 1; y++)
            {
                for (int x = 0; x < cols - 1; x++)
                {
                    int p1 = y * cols + x;
                    int p2 = p1 + 1;
                    int p3 = (y + 1) * cols + x;
                    int p4 = p3 + 1;

                    _indices.Add(p1); _indices.Add(p2); _indices.Add(p3);
                    _indices.Add(p2); _indices.Add(p4); _indices.Add(p3);
                }
            }

            // 🔹 Создаем CanvasGeometry через CanvasDevice.GetSharedDevice()
            var device = CanvasDevice.GetSharedDevice();

            for (int i = 0; i < _indices.Count; i += 3)
            {
                var p1 = _points[_indices[i]];
                var p2 = _points[_indices[i + 1]];
                var p3 = _points[_indices[i + 2]];

                using (var builder = new CanvasPathBuilder(device))
                {
                    builder.BeginFigure(p1);
                    builder.AddLine(p2);
                    builder.AddLine(p3);
                    builder.EndFigure(CanvasFigureLoop.Closed);

                    var geometry = CanvasGeometry.CreatePath(builder);
                    _triangleGeometries.Add(geometry);
                }
            }

            _geometryReady = true;
        }

        // 🔹 2. ОТРИСОВКА
        private void LowPolyCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (!_geometryReady || _triangleGeometries.Count == 0) return;

            var accentBrush = App.Current.Resources["AccentBrush"] as Windows.UI.Xaml.Media.SolidColorBrush;
            Color baseColor = accentBrush?.Color ?? Colors.Red;

            using (var ds = args.DrawingSession)
            {
                ds.Clear(Color.FromArgb(255, 10, 10, 10));

                foreach (var geometry in _triangleGeometries)
                {
                    float brightness = 0.2f + (float)(_rnd.NextDouble() * 0.8f);

                    Color triColor = Color.FromArgb(
                        255,
                        (byte)(baseColor.R * brightness),
                        (byte)(baseColor.G * brightness),
                        (byte)(baseColor.B * brightness)
                    );

                    ds.FillGeometry(geometry, triColor);
                    ds.DrawGeometry(geometry, Colors.Black, 0.5f);
                }
            }
        }

        // 🔹 3. Очистка при выгрузке
        private void LowPolyCanvas_Unloaded(object sender, object e)
        {
            foreach (var geo in _triangleGeometries)
            {
                geo.Dispose();
            }
            _triangleGeometries.Clear();
            _geometryReady = false;
        }

        // 🔹 4. При изменении размера
        private void LowPolyCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _geometryReady = false;
            // Пересоздаем ресурсы
            LowPolyCanvas.Invalidate();
        }
    }

}
