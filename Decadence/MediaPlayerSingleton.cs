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

        public static void PlayFile(StorageFile file)
        {
            if (file == null) return;

            CurrentFile = file;
            _mediaPlayer?.Pause();

            var source = Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
            Player.Source = source;
            Player.Play();
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