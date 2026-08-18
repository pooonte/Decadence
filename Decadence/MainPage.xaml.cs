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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Threading;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media;

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

        private bool _isVerticalSnapping = false;

        private double _verticalParallaxRatio = 0;

        private readonly DispatcherTimer _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.Loaded += MainPage_Loaded;
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
            MainPanorama.SizeChanged += MainPanorama_SizeChanged;
            Window.Current.SizeChanged += Window_SizeChanged;
            PageVerticalScroll.SizeChanged += PageVerticalScroll_SizeChanged;

            InitializePlayerIcons();

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
        private void PageVerticalScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVerticalSectionHeights();
        }
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePanoramaSectionWidths();
            UpdateVerticalSectionHeights();
            this.UpdateLayout();

            // Стартуем на "Главная/Настройки" (верх), плеер скрыт снизу
            PageVerticalScroll.ChangeView(null, 0, null, true);
        }

        private void UpdateVerticalSectionHeights()
        {
            double height = PageVerticalScroll.ActualHeight;
            if (height <= 0) return;

            PlayerSection.Height = height;
            MainPanorama.Height = height;

            this.UpdateLayout();
            LogVerticalDiagnostics("UpdateVerticalSectionHeights");
        }

        private void PageVerticalScroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            LogVerticalDiagnostics("ViewChanged");

            UpdatePlayerSectionActiveState();

            if (!e.IsIntermediate && !_isVerticalSnapping)
            {
                SnapVerticalToNearestSection();
            }
        }

        private void SnapVerticalToNearestSection()
        {
            double sectionHeight = PlayerSection.ActualHeight;
            if (sectionHeight <= 0) return;

            double currentOffset = PageVerticalScroll.VerticalOffset;
            double nearestSection = Math.Round(currentOffset / sectionHeight) * sectionHeight;

            if (Math.Abs(currentOffset - nearestSection) > 1)
            {
                _isVerticalSnapping = true;
                PageVerticalScroll.ChangeView(null, nearestSection, null, false);
                _isVerticalSnapping = false;
            }
        }

        private void UpdateVerticalSwipeAvailability()
        {
            bool isOnHome = MainPanorama.HorizontalOffset < HomeSection.ActualWidth / 2;
            PageVerticalScroll.VerticalScrollMode = isOnHome ? ScrollMode.Enabled : ScrollMode.Disabled;
        }
        private void MainPanorama_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePanoramaSectionWidths();
            UpdateVerticalSectionHeights();
        }

        private void UpdatePanoramaSectionWidths()
        {
            double width = MainPanorama.ActualWidth;
            if (width <= 0) return;

            HomeSection.Width = width;
            SettingButtonsPanel.Width = width;
            OnlineButtonsPanel.Width = width;   // ← новая строка, иначе будет та же чернота, что мы чинили раньше

            this.UpdateLayout();

            UpdateBackgroundParallax();
            SnapToNearestSection();
        }
        private void LogVerticalDiagnostics(string context)
        {
            System.Diagnostics.Debug.WriteLine(
                $"📐[{context}] PageVerticalScroll: ActualHeight={PageVerticalScroll.ActualHeight:F1}, " +
                $"ExtentHeight={PageVerticalScroll.ExtentHeight:F1}, ViewportHeight={PageVerticalScroll.ViewportHeight:F1}, " +
                $"VerticalOffset={PageVerticalScroll.VerticalOffset:F1} | " +
                $"MainPanorama.ActualHeight={MainPanorama.ActualHeight:F1} | " +
                $"PlayerSection.ActualHeight={PlayerSection.ActualHeight:F1}");
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

            UpdateVerticalSwipeAvailability();
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

        private void UpdateVerticalBackgroundParallax()
        {
            double screenHeight = Window.Current.Bounds.Height;
            double totalContentHeight = MainPanorama.ActualHeight + PlayerSection.ActualHeight;
            double backgroundHeight = PanoramaBackground.ActualHeight;

            double scrollableForeground = Math.Max(totalContentHeight - screenHeight, 1);
            double scrollableBackground = Math.Max(backgroundHeight - screenHeight, 0);

            _verticalParallaxRatio = scrollableBackground / scrollableForeground;
            ApplyParallaxOffset();
        }

        private void ApplyParallaxOffset()
        {
            PanoramaBackgroundTransform.TranslateX = -MainPanorama.HorizontalOffset * _parallaxRatio;
            PanoramaBackgroundTransform.TranslateY = -PageVerticalScroll.VerticalOffset * _verticalParallaxRatio; // 🔹 новое
        }

        private void DismissKeyboard()
        {
            bool wasEnabled = SearchBox.IsEnabled;
            SearchBox.IsEnabled = false;
            SearchBox.IsEnabled = wasEnabled;
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
            UpdateVerticalSectionHeights();
            this.UpdateLayout();

            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            Window.Current.CoreWindow.KeyDown += OnKeyDown;
            App.PlaylistsUpdated += OnPlaylistsUpdated;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            Window.Current.SizeChanged -= Window_SizeChanged;
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

        private void Window_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            UpdateBackgroundSize();
            UpdatePanoramaSectionWidths();
            UpdateVerticalSectionHeights();
            UpdateVerticalBackgroundParallax();
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
            bool started = await MediaPlayerSingleton.PlayAsync(track, playlist);
            if (!started)
            {
                var dialog = new Windows.UI.Popups.MessageDialog(
                    "Не удалось воспроизвести трек — файл не найден. Возможно, он был удалён или перемещён. Попробуйте обновить библиотеку в Настройках.",
                    "Ошибка воспроизведения");
                await dialog.ShowAsync();
                return;
            }
            OpenPlayerSection();
        }

        private void OpenPlayerSection()
        {
            LogFocusedElement("OpenPlayerSection — до DismissKeyboard");   // ← новая строка
            DismissKeyboard();
            LogFocusedElement("OpenPlayerSection — после DismissKeyboard");   // ← новая строка
            PageVerticalScroll.ChangeView(null, MainPanorama.ActualHeight, null, false);
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
            DismissKeyboard();
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
            DismissKeyboard();
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
            DismissKeyboard();
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
            DismissKeyboard();
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
            OpenPlayerSection();
        }

        private void PlaylistsPanelControl_PlaylistSelected(object sender, Playlist playlist)
        {
            if (playlist.Tracks.Count == 0) return;
            PlayAndOpenPlayer(playlist.Tracks[0], playlist.Tracks.ToList());
        }

        private async void PlaylistsPanelControl_PlaylistEditRequested(object sender, Playlist playlist)
        {
            var dialog = new ContentDialog
            {
                Title = "Редактировать плейлист",
                PrimaryButtonText = "Переименовать",
                SecondaryButtonText = "Порядок треков",
                CloseButtonText = "Закрыть"
            };

            var nameBox = new TextBox { Text = playlist.Name };
            dialog.Content = nameBox;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string newName = nameBox.Text.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != playlist.Name)
                {
                    playlist.Name = newName;
                    await PlaylistStorage.SavePlaylistsAsync(_playlists);
                    PlaylistsPanelControl.SetPlaylists(_playlists);
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await ShowReorderDialogAsync(playlist);
            }
        }

        private async Task ShowReorderDialogAsync(Playlist playlist)
        {
            var listBox = new ListBox { SelectionMode = SelectionMode.Single };
            foreach (var t in playlist.Tracks)
                listBox.Items.Add($"{t.Title} — {t.Artist}");

            var moveUpButton = new Button { Content = "▲ Вверх" };
            var moveDownButton = new Button { Content = "▼ Вниз" };

            moveUpButton.Click += (s, e) =>
            {
                int idx = listBox.SelectedIndex;
                if (idx > 0)
                {
                    var track = playlist.Tracks[idx];
                    playlist.Tracks.RemoveAt(idx);
                    playlist.Tracks.Insert(idx - 1, track);

                    var item = listBox.Items[idx];
                    listBox.Items.RemoveAt(idx);
                    listBox.Items.Insert(idx - 1, item);
                    listBox.SelectedIndex = idx - 1;
                }
            };

            moveDownButton.Click += (s, e) =>
            {
                int idx = listBox.SelectedIndex;
                if (idx >= 0 && idx < playlist.Tracks.Count - 1)
                {
                    var track = playlist.Tracks[idx];
                    playlist.Tracks.RemoveAt(idx);
                    playlist.Tracks.Insert(idx + 1, track);

                    var item = listBox.Items[idx];
                    listBox.Items.RemoveAt(idx);
                    listBox.Items.Insert(idx + 1, item);
                    listBox.SelectedIndex = idx + 1;
                }
            };

            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            buttonsPanel.Children.Add(moveUpButton);
            buttonsPanel.Children.Add(moveDownButton);

            var content = new StackPanel();
            content.Children.Add(listBox);
            content.Children.Add(buttonsPanel);

            var dialog = new ContentDialog
            {
                Title = $"Порядок треков — {playlist.Name}",
                PrimaryButtonText = "Сохранить",
                SecondaryButtonText = "Отмена",
                Content = content
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await PlaylistStorage.SavePlaylistsAsync(_playlists);

                var orderedIds = playlist.Tracks.Select(t => t.Id).ToList();
                await LibraryDatabase.ReorderPlaylistTracksAsync(playlist.Id, orderedIds);
            }
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

            // Пустой запрос — реагируем сразу, без задержки
            if (string.IsNullOrEmpty(query))
            {
                _searchDebounceTimer.Stop();
                MainButtonsPanel.Visibility = Visibility.Visible;
                SearchResults.Visibility = Visibility.Collapsed;
                return;
            }

            // Непустой запрос — ждём паузу в наборе перед реальным поиском
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private async void SearchDebounceTimer_Tick(object sender, object e)
        {
            _searchDebounceTimer.Stop();

            string query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) return;

            var records = await LibraryDatabase.SearchTracksAsync(query.ToLower());
            var results = records.Select(r => r.ToTrackItem()).ToList();

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

        private readonly List<QueueItem> _queueCache = new List<QueueItem>();

        private bool _userIsSeeking = false;
        private bool _wasPlayingBeforeSeek = false;
        private DispatcherTimer _globalTimer;
        private bool _isPlayerActive = false;
        private BitmapImage _playIcon;
        private BitmapImage _pauseIcon;
        private BitmapImage _repeatNoneIcon;
        private BitmapImage _repeatOneIcon;
        private BitmapImage _repeatAllIcon;
        private BitmapImage _currentAlbumArt;

        private double _swipeThreshold = 80;
        private bool _isSwiping = false;

        private void InitializePlayerIcons()
        {
            _playIcon = new BitmapImage(new Uri("ms-appx:///Assets/play_white.png"));
            _pauseIcon = new BitmapImage(new Uri("ms-appx:///Assets/pause_white.png"));
            _repeatNoneIcon = new BitmapImage(new Uri("ms-appx:///Assets/repeat_none_white.png"));
            _repeatOneIcon = new BitmapImage(new Uri("ms-appx:///Assets/repeat_one_white.png"));
            _repeatAllIcon = new BitmapImage(new Uri("ms-appx:///Assets/repeat_all_white.png"));
        }

        private string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

        // ===== Активация/деактивация по видимости секции плеера =====

        private void UpdatePlayerSectionActiveState()
        {
            double sectionHeight = MainPanorama.ActualHeight;
            if (sectionHeight <= 0) return;

            bool isPlayerVisible = PageVerticalScroll.VerticalOffset >= sectionHeight / 2;

            if (isPlayerVisible && !_isPlayerActive)
            {
                _isPlayerActive = true;
                ActivatePlayerSection();
            }
            else if (!isPlayerVisible && _isPlayerActive)
            {
                _isPlayerActive = false;
                DeactivatePlayerSection();
            }
        }

        private async void ActivatePlayerSection()
        {
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
            _globalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _globalTimer.Tick += GlobalTimer_Tick;
            _globalTimer.Start();

            UpdateQueue();
            ForceUpdatePosition();
        }

        private void DeactivatePlayerSection()
        {
            MediaPlayerSingleton.TrackChanged -= MediaPlayerSingleton_TrackChanged;
            MediaPlayerSingleton.PlaybackStateChanged -= MediaPlayerSingleton_PlaybackStateChanged;

            if (_globalTimer != null)
            {
                _globalTimer.Stop();
                _globalTimer.Tick -= GlobalTimer_Tick;
                _globalTimer = null;
            }
        }

        private void GlobalTimer_Tick(object sender, object e)
        {
            if (_isPlayerActive) UpdatePlaybackPosition();
        }

        private void MediaPlayerSingleton_TrackChanged(object sender, TrackItem track)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!_isPlayerActive) return;
                _ = ShowTrackAsync(track);
                UpdateQueue();
                ForceUpdatePosition();
            });
        }

        private void MediaPlayerSingleton_PlaybackStateChanged(object sender, bool isPlaying)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!_isPlayerActive) return;
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
                case RepeatMode.None: MediaPlayerSingleton.RepeatMode = RepeatMode.One; break;
                case RepeatMode.One: MediaPlayerSingleton.RepeatMode = RepeatMode.All; break;
                case RepeatMode.All: MediaPlayerSingleton.RepeatMode = RepeatMode.None; break;
            }
            UpdateRepeatButtonIcon();
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.ToggleShuffle();
            UpdateShuffleButtonState();
            UpdateQueue();
        }

        // ===== Инфо / избранное / плейлисты =====

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

        private async void AddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var track = MediaPlayerSingleton.CurrentTrack;
            if (track == null)
            {
                await new MessageDialog("Нет активного трека").ShowAsync();
                return;
            }

            var playlists = App.CurrentPlaylists;
            if (playlists == null || playlists.Count == 0)
            {
                await new MessageDialog("Нет плейлистов. Создайте первый в главном меню.").ShowAsync();
                return;
            }

            var options = playlists.Select(p => p.Name).ToArray();
            var selectedName = await ShowPlaylistPickerForPlayer(options);

            if (selectedName != null)
            {
                var playlist = playlists.First(p => p.Name == selectedName);
                if (!playlist.Tracks.Any(t => t.FilePath == track.FilePath))
                {
                    playlist.Tracks.Add(track);
                    await PlaylistStorage.SavePlaylistsAsync(playlists);
                    await new MessageDialog($"Трек добавлен в плейлист \"{playlist.Name}\"").ShowAsync();
                    App.NotifyPlaylistsUpdated();
                }
                else
                {
                    await new MessageDialog("Трек уже есть в этом плейлисте").ShowAsync();
                }
            }
        }

        private async Task<string> ShowPlaylistPickerForPlayer(string[] options)
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

        // ===== Очередь =====

        private void UpdateQueue()
        {
            _queueCache.Clear();
            var playlist = MediaPlayerSingleton.CurrentPlaylist;
            int currentIndex = MediaPlayerSingleton.CurrentIndex;

            for (int i = 0; i < playlist.Count; i++)
            {
                var track = playlist[i];
                _queueCache.Add(new QueueItem
                {
                    Index = i + 1,
                    Title = track.Title,
                    Artist = track.Artist,
                    DurationText = FormatTime(track.Duration),
                    IsCurrent = i == currentIndex
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
        private void LogFocusedElement(string context)
        {
            var focused = FocusManager.GetFocusedElement() as FrameworkElement;
            System.Diagnostics.Debug.WriteLine(
                $"🔎[{context}] Фокус на: {focused?.GetType().Name ?? "null"}, Name={focused?.Name ?? "—"}");
        }
        private void QueueListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is QueueItem item && args.ItemContainer.ContentTemplateRoot is Grid root)
            {
                var indexText = root.FindName("IndexText") as TextBlock;
                var titleText = root.FindName("TitleText") as TextBlock;

                if (indexText != null)
                    indexText.Text = item.IsCurrent ? "▶" : item.Index.ToString();

                if (titleText != null)
                    titleText.Foreground = item.IsCurrent
                        ? (Brush)Application.Current.Resources["AccentBrush"]
                        : (Brush)Application.Current.Resources["TextBrush"];
            }
        }
    }
}
