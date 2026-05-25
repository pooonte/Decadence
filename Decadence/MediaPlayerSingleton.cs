using Windows.Media.Playback;
using Windows.Storage;

namespace Singleton
{
    public static class MediaPlayerSingleton
    {
        private static MediaPlayer _mediaPlayer;
        private static StorageFile _currentFile;

        public static MediaPlayer Player
        {
            get
            {
                if (_mediaPlayer == null)
                {
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.AutoPlay = false;
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

        private static Windows.Media.Core.MediaSource _currentSource;

        public static void PlayFile(StorageFile file)
        {
            if (file == null) return;

            _mediaPlayer?.Pause();

            // 🔹 Освобождаем старый источник ПЕРЕД созданием нового
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
        public static void TogglePlayPause()
        {
            if (_mediaPlayer == null) return;

            if (IsPlaying)
            {
                _mediaPlayer.Pause();
            }
            else
            {
                _mediaPlayer.Play();
            }
        }
    }
}