using System.Collections.Generic;

namespace Decadence.Models
{
    public class AlbumItem
    {
        public string Name { get; set; }
        public string Artist { get; set; } // Добавляем Artist
        public TrackItem FirstTrack { get; set; }
        public int TrackCount { get; set; } // Добавляем TrackCount
        public List<TrackItem> Tracks { get; set; } = new List<TrackItem>();
    }
}