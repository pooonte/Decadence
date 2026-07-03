using SQLite;

namespace Decadence.Models
{
    [Table("Playlists")]
    public class PlaylistRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull]
        public string Name { get; set; }

        public string CoverPath { get; set; }
    }

    [Table("PlaylistTracks")]
    public class PlaylistTrackRecord
    {
        [Indexed]
        public int PlaylistId { get; set; }

        [Indexed]
        public int TrackId { get; set; }

        public int Position { get; set; }
    }
}