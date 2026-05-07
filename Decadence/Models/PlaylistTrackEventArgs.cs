using System;

namespace Decadence.Models
{
    public class PlaylistTrackEventArgs : EventArgs
    {
        public Playlist Playlist { get; set; }
        public TrackItem Track { get; set; }
    }
}