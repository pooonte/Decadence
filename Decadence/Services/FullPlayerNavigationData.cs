using System.Collections.Generic;
using Decadence.Models;

namespace Decadence
{
    public enum RepeatMode { None, One, All }

    public class FullPlayerNavigationData
    {
        public TrackItem Track { get; set; }
        public List<TrackItem> Playlist { get; set; }
        public int PlaylistIndex { get; set; }
        public RepeatMode CurrentRepeatMode { get; set; }
    }
}