using System.Collections.ObjectModel;
using Decadence.Models;

namespace Decadence.Services
{
    public static class MusicLibrary
    {
        public static ObservableCollection<TrackItem> Tracks { get; set; } = new ObservableCollection<TrackItem>();
        public static ObservableCollection<ArtistItem> Artists { get; set; } = new ObservableCollection<ArtistItem>();
        public static ObservableCollection<AlbumItem> Albums { get; set; } = new ObservableCollection<AlbumItem>();
    }
}