using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decadence.Models
{
    public class Playlist
    {
        public string Name { get; set; }
        public List<TrackItem> Tracks { get; set; }
        public int TrackCount => Tracks?.Count ?? 0;
        public string CoverPath { get; set; } // опционально: обложка
    }
}
