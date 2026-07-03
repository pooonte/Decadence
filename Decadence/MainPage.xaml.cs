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
using System.Threading;

namespace Decadence
{
    public sealed partial class MainPage : Page
    {
        private bool _isSnapping = false;

        private static bool _libraryInitialized = false;
        private CancellationTokenSource _bgCheckCts;

        private ObservableCollection<TrackItem> _tracks = new ObservableCollection<TrackItem>();
        private ObservableCollection<ArtistItem> _artists = new ObservableCollection<ArtistItem>();
        private ObservableCollection<AlbumItem> _albums = new ObservableCollection<AlbumItem>();

        private bool _isLoading = false;
        private bool _isInitialized = false;
        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.Loaded += MainPage_Loaded;
            MainPanorama.SizeChanged += MainPanorama_SizeChanged;

            if (!_libraryInitialized && _tracks.Count == 0)
            {
                _ = InitializeLibraryAsync();
                _libraryInitialized = true;
            }

            _ = LoadPlaylists();

            PlaylistsPanelControl.CreatePlaylist += (s, name) => CreatePlaylist(name);
            TracksPanelControl.AddToPlaylistRequested += TracksPanelControl_AddToPlaylistRequested;
            PlaylistsPanelControl.RemoveTrackRequested += PlaylistsPanelControl_RemoveTrackRequested;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBackgroundSize();
            UpdatePanoramaSectionWidths();
        }

        private void MainPanorama_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBackgroundSize();
            UpdatePanoramaSectionWidths();
        }

        private void UpdatePanoramaSectionWidths()
        {
            double width = MainPanorama.ActualWidth;
            if (width <= 0) return;

            HomeSection.Width = width;
            SettingButtonsPanel.Width = width;
            OnlineButtonsPanel.Width = width;

            this.UpdateLayout();

            UpdateBackgroundParallax();
            SnapToNearestSection();   // сразу подправит текущее положение, если ресайз застал вид "между" секциями
        }


        private void PanoramaBackground_ImageOpened(object sender, RoutedEventArgs e)
        {
            if (PanoramaBackground.Source is Windows.UI.Xaml.Media.Imaging.BitmapImage bmp)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"🖼️ Исходный файл: {bmp.PixelWidth}×{bmp.PixelHeight}px | " +
                    $"Отрендерено: {PanoramaBackground.ActualWidth:F0}×{PanoramaBackground.ActualHeight:F0} | " +
                    $"Ширина экрана: {Window.Current.Bounds.Width:F0}, высота: {Window.Current.Bounds.Height:F0}");
            }

            UpdateBackgroundParallax();
        }

        private void MainPanorama_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ApplyParallaxOffset();

            if (!e.IsIntermediate && !_isSnapping)
            {
                SnapToNearestSection();
            }
        }

        private void SnapToNearestSection()
        {
            double sectionWidth = HomeSection.ActualWidth;
            if (sectionWidth <= 0) return;

            double currentOffset = MainPanorama.HorizontalOffset;
            double nearestSection = Math.Round(currentOffset / sectionWidth) * sectionWidth;

            if (Math.Abs(currentOffset - nearestSection) > 1)
            {
                _isSnapping = true;
                MainPanorama.ChangeView(nearestSection, null, null, false);
                _isSnapping = false;
            }
        }
        private void UpdateBackgroundSize()
        {
            double screenHeight = Window.Current.Bounds.Height;
            if (screenHeight > 0)
                PanoramaBackground.Height = screenHeight;
        }

        private double _parallaxRatio = 0.5; // запасное значение, пока не посчитан реальный

        private void UpdateBackgroundParallax()
        {
            double screenWidth = Window.Current.Bounds.Width;
            double totalContentWidth = PanoramaSections.ActualWidth;
            double backgroundWidth = PanoramaBackground.ActualWidth;

            double scrollableForeground = Math.Max(totalContentWidth - screenWidth, 1);
            double scrollableBackground = Math.Max(backgroundWidth - screenWidth, 0);

            _parallaxRatio = scrollableBackground / scrollableForeground;

            System.Diagnostics.Debug.WriteLine(
    $"🎛️ totalContentWidth={totalContentWidth:F0} (ожидается ~720), " +
    $"backgroundWidth={backgroundWidth:F0}, screenWidth={screenWidth:F0}, " +
    $"scrollableForeground={scrollableForeground:F0}, scrollableBackground={scrollableBackground:F0}, " +
    $"ratio={_parallaxRatio:F3}");

            ApplyParallaxOffset();
        }

        private void ApplyParallaxOffset()
        {
            PanoramaBackgroundTransform.TranslateX = -MainPanorama.HorizontalOffset * _parallaxRatio;
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
                TracksPanelControl.Clear();
                if (TracksPanelControl.Parent is Panel p) p.Children.Remove(TracksPanelControl);
                anyPanelClosed = true;
            }
            if (ArtistsPanelControl.IsVisible)
            {
                ArtistsPanelControl.Hide();
                ArtistsPanelControl.Clear();
                if (ArtistsPanelControl.Parent is Panel p) p.Children.Remove(ArtistsPanelControl);
                anyPanelClosed = true;
            }
            if (AlbumsPanelControl.IsVisible)
            {
                AlbumsPanelControl.Hide();
                AlbumsPanelControl.Clear();
                if (AlbumsPanelControl.Parent is Panel p) p.Children.Remove(AlbumsPanelControl);
                anyPanelClosed = true;
            }
            if (PlaylistsPanelControl.IsVisible)
            {
                PlaylistsPanelControl.Hide();
                PlaylistsPanelControl.Clear();
                if (PlaylistsPanelControl.Parent is Panel p) p.Children.Remove(PlaylistsPanelControl);
                anyPanelClosed = true;
            }

            return anyPanelClosed;
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            UpdateBackgroundSize();
            UpdatePanoramaSectionWidths();

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            Window.Current.CoreWindow.KeyDown += OnKeyDown;
            App.PlaylistsUpdated += OnPlaylistsUpdated;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // 1. Отписка от глобальных событий (СТРОГО ДО base)
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            Window.Current.CoreWindow.KeyDown -= OnKeyDown;
            App.PlaylistsUpdated -= OnPlaylistsUpdated;

            // 2. Очищаем привязки только тех элементов, которые реально есть на MainPage
            if (SearchResults != null) SearchResults.ItemsSource = null;

            // 3. Скрываем панели (внутренняя очистка ListView уже реализована тобой в контролах)
            TracksPanelControl?.Hide();
            ArtistsPanelControl?.Hide();
            AlbumsPanelControl?.Hide();
            PlaylistsPanelControl?.Hide();

            // 4. Сбрасываем DataContext страницы
            this.DataContext = null;

            base.OnNavigatingFrom(e);
        }
        private async Task InitializeLibraryAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("=== ИНИЦИАЛИЗАЦИЯ БИБЛИОТЕКИ ===");

                int existingCount = await LibraryDatabase.GetTrackCountAsync();

                if (existingCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine("📖 Загрузка из базы...");
                    var records = await LibraryDatabase.GetAllTracksAsync();
                    ShowTracksFromRecords(records);

                    System.Diagnostics.Debug.WriteLine("🔄 Фоновая проверка обновлений...");

                    _bgCheckCts?.Cancel();
                    _bgCheckCts = new System.Threading.CancellationTokenSource();
                    var token = _bgCheckCts.Token;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            bool changed = await LibraryService.QuickCheckAsync();
                            if (!token.IsCancellationRequested && changed)
                            {
                                var updated = await LibraryDatabase.GetAllTracksAsync();
                                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    ShowTracksFromRecords(updated);
                                });
                            }
                        }
                        catch (OperationCanceledException) { /* Задачу отменили — нормально */ }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"⚠️ Фоновая проверка упала: {ex.Message}"); }
                    }, token);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("🔍 Первый запуск, сканирование...");
                    ShowLoadingIndicator(true);
                    await LibraryService.FullScanAsync();
                    var records = await LibraryDatabase.GetAllTracksAsync();
                    ShowTracksFromRecords(records);
                    ShowLoadingIndicator(false);
                }

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("=== ИНИЦИАЛИЗАЦИЯ ЗАВЕРШЕНА ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Критическая ошибка загрузки: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }
        private void ShowLoadingIndicator(bool show)
        {
            if (MainPanorama != null)
                MainPanorama.IsEnabled = !show;
        }
        private void ShowTracksFromRecords(List<TrackRecord> records)
        {
            _tracks.Clear();
            _artists.Clear();
            _albums.Clear();

            var artistDict = new Dictionary<string, List<TrackItem>>();
            var albumDict = new Dictionary<string, List<TrackItem>>();

            foreach (var record in records)
            {
                var track = new TrackItem
                {
                    Id = record.Id,
                    FilePath = record.FilePath,
                    Title = record.Title,
                    Artist = record.Artist,
                    Album = record.Album,
                    Genre = record.Genre,
                    TrackNumber = record.TrackNumber,
                    IsFavorite = record.IsFavorite,   // ← эта строка должна быть ЗДЕСЬ, внутри foreach
                    Duration = TimeSpan.FromMilliseconds(record.DurationMs)
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
        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // Заглушка
            var dialog = new Windows.UI.Popups.MessageDialog("Decadence\nВерсия 0.3\nМузыкальный плеер", "О программе");
            _ = dialog.ShowAsync();
        }

        private async void PlayAndOpenPlayer(TrackItem track, List<TrackItem> playlist)
        {
            await MediaPlayerSingleton.PlayAsync(track, playlist);
            Frame.Navigate(typeof(PlayerMenu));
        }

        private async void RefreshLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            try
            {
                var dialog = new Windows.UI.Popups.MessageDialog(
                    "Обновление библиотеки пересканирует все музыкальные файлы. Это может занять несколько минут. Продолжить?",
                    "Обновление библиотеки");

                dialog.Commands.Add(new Windows.UI.Popups.UICommand("Да") { Id = 0 });
                dialog.Commands.Add(new Windows.UI.Popups.UICommand("Нет") { Id = 1 });

                var result = await dialog.ShowAsync();
                if ((int)result.Id == 0)
                    await RefreshLibraryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ КРАШ В RefreshLibrary: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            }
        }

        private async Task RefreshLibraryAsync()
        {
            try
            {
                _libraryInitialized = false;
                _isLoading = true;
                ShowLoadingIndicator(true);

                await LibraryService.FullScanAsync();
                var records = await LibraryDatabase.GetAllTracksAsync();
                ShowTracksFromRecords(records);

                try
                {
                    var completeDialog = new Windows.UI.Popups.MessageDialog(
                        $"Библиотека обновлена. Найдено {records.Count} треков.", "Готово");
                    await completeDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Не удалось показать диалог: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ КРАШ В RefreshLibraryAsync: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
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
            var track = MediaPlayerSingleton.CurrentTrack;
            if (track == null) return;

            var dialog = new Windows.UI.Popups.MessageDialog(
                $"Название: {track.Title}\n" +
                $"Исполнитель: {track.Artist}\n" +
                $"Альбом: {track.Album}\n" +
                $"Длительность: {track.Duration:mm\\:ss}\n" +
                $"Путь: {track.FilePath}",
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
        private void TracksPanelControl_TrackClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                TracksPanelControl.Hide();
                PlayAndOpenPlayer(track, _tracks.ToList());
            }
        }
        private void TracksButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tracks.Count > 0)
            {
                if (TracksPanelControl.Parent is Panel parent)
                    parent.Children.Remove(TracksPanelControl);

                // 🔹 Явно задаём RowSpan перед добавлением
                Grid.SetRowSpan(TracksPanelControl, 2);
                MainContainer.Children.Add(TracksPanelControl);
                TracksPanelControl.SetTracks(_tracks);
                TracksPanelControl.Show();
            }
        }
        // Обработчик кнопки назад
        private void TracksPanelControl_BackClicked(object sender, EventArgs e)
        {
            TracksPanelControl.Hide();
            TracksPanelControl.Clear();
            MainContainer.Children.Remove(TracksPanelControl);
        }
        private void ArtistsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_artists.Count > 0)
            {
                if (ArtistsPanelControl.Parent is Panel parent)
                    parent.Children.Remove(ArtistsPanelControl);

                Grid.SetRowSpan(ArtistsPanelControl, 2);
                MainContainer.Children.Add(ArtistsPanelControl);
                ArtistsPanelControl.SetArtists(_artists);
                ArtistsPanelControl.Show();
            }
        }

        private void AlbumsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_albums.Count > 0)
            {
                if (AlbumsPanelControl.Parent is Panel parent)
                    parent.Children.Remove(AlbumsPanelControl);

                Grid.SetRowSpan(AlbumsPanelControl, 2);
                MainContainer.Children.Add(AlbumsPanelControl);
                AlbumsPanelControl.SetAlbums(_albums);
                AlbumsPanelControl.Show();
            }
        }

        private void PlaylistsButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistsPanelControl.Parent is Panel parent)
                parent.Children.Remove(PlaylistsPanelControl);

            Grid.SetRowSpan(PlaylistsPanelControl, 2);
            MainContainer.Children.Add(PlaylistsPanelControl);
            PlaylistsPanelControl.SetPlaylists(_playlists);
            PlaylistsPanelControl.Show();
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
        private void ArtistsPanelControl_TrackClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                var playlist = _tracks.Where(t => t.Artist == track.Artist).ToList();
                ArtistsPanelControl.Hide();
                PlayAndOpenPlayer(track, playlist);
            }
        }

        // Обработчик кнопки "Назад"
        private void ArtistsPanelControl_BackClicked(object sender, EventArgs e)
        {
            ArtistsPanelControl.Hide();
            ArtistsPanelControl.Clear();
            MainContainer.Children.Remove(ArtistsPanelControl);
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
        private void AlbumsPanelControl_TrackClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                var playlist = _tracks.Where(t => t.Album == track.Album).ToList();
                AlbumsPanelControl.Hide();
                PlayAndOpenPlayer(track, playlist);
            }
        }

        private void AlbumsPanelControl_BackClicked(object sender, EventArgs e)
        {
            AlbumsPanelControl.Hide();
            AlbumsPanelControl.Clear();
            MainContainer.Children.Remove(AlbumsPanelControl);
        }

        // Обновление счетчика (вызывай после загрузки треков)
        private void UpdateTrackCount()
        {
            TrackCountNumber.Text = _tracks.Count.ToString();
        }

        // Переход в PlayerMenu
        private void StatsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(PlayerMenu));
        }

        private void PlaylistsPanelControl_PlaylistSelected(object sender, Playlist playlist)
        {
            if (playlist.Tracks.Count == 0) return;
            PlayAndOpenPlayer(playlist.Tracks[0], playlist.Tracks.ToList());
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
            var track = MediaPlayerSingleton.CurrentTrack;
            if (track == null) return;

            if (!playlist.Tracks.Any(t => t.FilePath == track.FilePath))
            {
                playlist.Tracks.Add(track);
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
        private void SearchResult_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                PlayAndOpenPlayer(track, _tracks.ToList());

                // Очищаем поиск
                SearchBox.Text = "";
                SearchResults.Visibility = Visibility.Collapsed;
                MainButtonsPanel.Visibility = Visibility.Visible;
            }
        }
    }
}
