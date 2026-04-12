using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Decadence.Models
{
    class GenreItem
    {
        public string Name { get; set; }
        public int TrackCount { get; set; }
        public List<TrackItem> Tracks { get; set; }
        public string Icon { get; set; }
    }
}
