using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace Decadence.Services
{
    public static class MusicTagHelper
    {
        public static async Task<MusicInfo> GetMusicInfo(StorageFile file)
        {
            try
            {
                var musicProperties = await file.Properties.GetMusicPropertiesAsync();

                return new MusicInfo
                {
                    Title = !string.IsNullOrEmpty(musicProperties.Title)
                           ? musicProperties.Title
                           : System.IO.Path.GetFileNameWithoutExtension(file.Name),

                    Artist = !string.IsNullOrEmpty(musicProperties.Artist)
                            ? musicProperties.Artist
                            : "Неизвестный исполнитель",

                    Album = musicProperties.Album ?? "Неизвестный альбом",
                    TrackNumber = musicProperties.TrackNumber,
                    Duration = musicProperties.Duration,
                    FilePath = file.Path,
                    File = file
                };
            }
            catch
            {
                return new MusicInfo
                {
                    Title = System.IO.Path.GetFileNameWithoutExtension(file.Name),
                    Artist = "Неизвестный исполнитель",
                    Album = "Неизвестный альбом",
                    FilePath = file.Path,
                    File = file
                };
            }
        }
    }

    // Класс для хранения метаданных
    public class MusicInfo
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public uint TrackNumber { get; set; }
        public TimeSpan Duration { get; set; }
        public string FilePath { get; set; }
        public StorageFile File { get; set; }
    }
}