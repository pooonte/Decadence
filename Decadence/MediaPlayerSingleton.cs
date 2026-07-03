using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Storage;
using Decadence.Models;
using Decadence;

namespace Singleton
{
    public static class MediaPlayerSingleton
    {
        private static MediaPlayer _mediaPlayer;
        private static StorageFile _currentFile;
        private static Windows.Media.Core.MediaSource _currentSource;

        // ===== То, что уже было — не трогаем, чтобы не сломать существующие вызовы =====

        public static MediaPlayer Player
        {
            get
            {
                if (_mediaPlayer == null)
                {
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.AutoPlay = false;
                    _mediaPlayer.PlaybackSession.PlaybackStateChanged += (s, e) =>
                        PlaybackStateChanged?.Invoke(null, IsPlaying);
                    _mediaPlayer.MediaEnded += async (s, e) => await NextAsync();
                }
                return _mediaPlayer;
            }
        }

        public static StorageFile CurrentFile
        {
            get => _currentFile;
            set => _currentFile = value;
        }

        public static bool IsPlaying =>
            _mediaPlayer?.PlaybackSession?.PlaybackState == MediaPlaybackState.Playing;

        public static void PlayFile(StorageFile file)
        {
            if (file == null) return;

            _mediaPlayer?.Pause();

            if (_currentSource != null)
            {
                _currentSource.Dispose();
                _currentSource = null;
            }

            CurrentFile = file;
            _currentSource = Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
            Player.Source = _currentSource;
            Player.Play();
        }

        public static void TogglePlayPause()
        {
            if (_mediaPlayer == null) return;
            if (IsPlaying) _mediaPlayer.Pause();
            else _mediaPlayer.Play();
        }

        // ВАЖНО: больше не зовём это из App.OnSuspending — иначе фоновое аудио не заработает.
        // Оставляем только для реального завершения приложения, если вообще понадобится.
        public static void Shutdown()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Source = null;
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }
            if (_currentSource != null)
            {
                _currentSource.Dispose();
                _currentSource = null;
            }
            _currentFile = null;
        }

        // ===== Новое: общее состояние трека/плейлиста/повтора/перемешивания =====

        private static List<TrackItem> _unshuffledPlaylist;
        private static readonly Random _random = new Random();

        public static TrackItem CurrentTrack { get; private set; }
        public static List<TrackItem> CurrentPlaylist { get; private set; } = new List<TrackItem>();
        public static int CurrentIndex { get; private set; } = -1;
        public static RepeatMode RepeatMode { get; set; } = RepeatMode.None;
        public static bool IsShuffleEnabled { get; private set; }

        public static event EventHandler<TrackItem> TrackChanged;
        public static event EventHandler<bool> PlaybackStateChanged;

        public static async Task PlayAsync(TrackItem track, List<TrackItem> playlist = null)
        {
            if (track == null) return;

            if (playlist != null)
            {
                CurrentPlaylist = playlist;
                _unshuffledPlaylist = new List<TrackItem>(playlist);
            }
            CurrentIndex = CurrentPlaylist.IndexOf(track);

            var file = track.File ?? await StorageFile.GetFileFromPathAsync(track.FilePath);
            track.File = file;
            CurrentTrack = track;

            PlayFile(file);

            var saved = ApplicationData.Current.LocalSettings.Values["SavedVolume"];
            Player.Volume = saved is double v ? v : 1.0;

            TrackChanged?.Invoke(null, track);
        }

        public static async Task NextAsync()
        {
            if (CurrentPlaylist.Count == 0) return;

            if (RepeatMode == RepeatMode.One)
            {
                await PlayAsync(CurrentTrack);
                return;
            }

            int next = CurrentIndex + 1;
            if (next >= CurrentPlaylist.Count)
            {
                if (RepeatMode != RepeatMode.All) { Player.Pause(); return; }
                next = 0;
            }
            await PlayAsync(CurrentPlaylist[next]);
        }

        public static async Task PreviousAsync()
        {
            if (CurrentPlaylist.Count == 0) return;

            var session = Player.PlaybackSession;
            if (RepeatMode == RepeatMode.One && session?.Position.TotalSeconds > 3)
            {
                session.Position = TimeSpan.Zero;
                return;
            }

            int prev = CurrentIndex - 1;
            if (prev < 0)
            {
                if (RepeatMode != RepeatMode.All) return;
                prev = CurrentPlaylist.Count - 1;
            }
            await PlayAsync(CurrentPlaylist[prev]);
        }

        public static void ToggleShuffle()
        {
            IsShuffleEnabled = !IsShuffleEnabled;
            if (CurrentTrack == null) return;

            if (IsShuffleEnabled)
            {
                _unshuffledPlaylist = new List<TrackItem>(CurrentPlaylist);
                var rest = CurrentPlaylist.Where(t => t != CurrentTrack)
                                          .OrderBy(_ => _random.Next()).ToList();
                CurrentPlaylist = new List<TrackItem> { CurrentTrack }.Concat(rest).ToList();
                CurrentIndex = 0;
            }
            else if (_unshuffledPlaylist != null)
            {
                CurrentPlaylist = _unshuffledPlaylist;
                CurrentIndex = CurrentPlaylist.IndexOf(CurrentTrack);
            }
        }
    }
}