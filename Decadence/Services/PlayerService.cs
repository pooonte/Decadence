using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Singleton;
using Decadence.Models;
using System.Collections.ObjectModel;

namespace Decadence.Services
{
    public static class PlayerService
    {
        public static event EventHandler<TrackItem> TrackChanged;
        public static event EventHandler<bool> PlaybackStateChanged;

        private static DispatcherTimer _positionTimer;
        private static int _currentTrackIndex = -1;
        private static ObservableCollection<TrackItem> _tracks;

        public static void Initialize(ObservableCollection<TrackItem> tracks)
        {
            _tracks = tracks;
            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        }

        public static async Task PlayTrack(StorageFile file, TrackItem track,
            TextBlock miniTitle, TextBlock miniArtist,
            TextBlock fullTitle, TextBlock fullArtist,
            Image miniArt, Image fullArt)
        {
            try
            {
                if (file == null || track == null) return;

                _currentTrackIndex = _tracks.IndexOf(track);
                track.File = file;

                MediaPlayerSingleton.PlayFile(file);

                var saved = ApplicationData.Current.LocalSettings.Values["SavedVolume"];
                MediaPlayerSingleton.Player.Volume = saved is double v ? v : 1.0;

                // Обновляем UI
                miniTitle.Text = track.Title;
                miniArtist.Text = track.Artist;
                fullTitle.Text = track.Title;
                fullArtist.Text = track.Artist;

                // Загружаем обложку
                await LoadAlbumArtAsync(file, miniArt, fullArt);

                TrackChanged?.Invoke(null, track);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка воспроизведения: {ex.Message}");
            }
        }

        private static async Task LoadAlbumArtAsync(StorageFile file, Image miniArt, Image fullArt)
        {
            try
            {
                var thumb = await file.GetThumbnailAsync(
                    Windows.Storage.FileProperties.ThumbnailMode.MusicView, 256);
                if (thumb != null && thumb.Size > 0)
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(thumb);
                    miniArt.Source = bitmap;
                    fullArt.Source = bitmap;
                }
            }
            catch { }
        }

        private static void OnPlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            bool isPlaying = MediaPlayerSingleton.IsPlaying;
            PlaybackStateChanged?.Invoke(null, isPlaying);
        }

        public static void TogglePlayPause()
        {
            MediaPlayerSingleton.TogglePlayPause();
        }

        public static bool IsPlaying => MediaPlayerSingleton.IsPlaying;

        public static void Next(ObservableCollection<TrackItem> tracks, ref int currentIndex)
        {
            if (tracks.Count == 0 || currentIndex < 0) return;
            currentIndex = Math.Min(tracks.Count - 1, currentIndex + 1);
        }

        public static void Previous(ObservableCollection<TrackItem> tracks, ref int currentIndex)
        {
            if (tracks.Count == 0 || currentIndex < 0) return;
            currentIndex = Math.Max(0, currentIndex - 1);
        }

        public static void StartPositionTimer(Slider progressSlider, TextBlock currentTime, TextBlock totalTime)
        {
            if (_positionTimer != null) return;

            _positionTimer = new DispatcherTimer();
            _positionTimer.Interval = TimeSpan.FromMilliseconds(500);
            _positionTimer.Tick += (s, e) =>
            {
                var session = MediaPlayerSingleton.Player.PlaybackSession;
                if (session?.NaturalDuration > TimeSpan.Zero)
                {
                    progressSlider.Maximum = session.NaturalDuration.TotalSeconds;
                    totalTime.Text = FormatTime(session.NaturalDuration);
                    progressSlider.Value = session.Position.TotalSeconds;
                    currentTime.Text = FormatTime(session.Position);
                }
            };
            _positionTimer.Start();
        }

        private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
    }
}