using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decadence.Models
{
    public class Playlist
    {
        public int Id { get; set; }   // 0 = ещё не сохранён в базе
        public string Name { get; set; }
        public List<TrackItem> Tracks { get; set; } = new List<TrackItem>();
        public int TrackCount => Tracks?.Count ?? 0;
        public string CoverPath { get; set; }
    }
}
