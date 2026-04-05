using System;
using Windows.Storage;
using Windows.Storage.FileProperties;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Decadence.Models
{
    [DataContract]
    public class TrackItem
    {
        [IgnoreDataMember]
        public StorageFile File { get; set; }

        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public string Artist { get; set; }

        [DataMember]
        public string Album { get; set; }

        [IgnoreDataMember]
        public TimeSpan Duration { get; set; }

        public int TrackNumber { get; set; }
        public string DurationString => $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}";
        // ДЛЯ АЛФАВИТНОЙ НАВИГАЦИИ - ПЕРВАЯ БУКВА
        public string FirstLetter
        {
            get
            {
                if (string.IsNullOrEmpty(Title)) return "#";
                char first = char.ToUpper(Title[0]);
                return char.IsLetter(first) ? first.ToString() : "#";
            }
        }

        // ДЛЯ АЛФАВИТНОЙ НАВИГАЦИИ - ПОКАЗЫВАТЬ БУКВУ-РАЗДЕЛИТЕЛЬ
        public bool ShowLetter { get; set; } // ЭТО СВОЙСТВО НУЖНО ДОБАВИТЬ

        public static async Task<TrackItem> FromFile(StorageFile file)
        {
            var track = new TrackItem
            {
                File = file,
                FilePath = file.Path
            };

            var props = await file.Properties.GetMusicPropertiesAsync();
            track.Title = string.IsNullOrEmpty(props.Title) ? file.DisplayName : props.Title;
            track.Artist = string.IsNullOrEmpty(props.Artist) ? "Неизвестный исполнитель" : props.Artist;
            track.Album = string.IsNullOrEmpty(props.Album) ? "Неизвестный альбом" : props.Album;
            track.Duration = props.Duration;

            return track;
        }
    }
}