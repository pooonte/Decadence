using SQLite;

namespace Decadence.Models
{
    [Table("Tracks")]
    public class TrackRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Unique, NotNull]
        public string FilePath { get; set; }

        [Indexed]
        public string Title { get; set; }

        [Indexed]
        public string Artist { get; set; }

        [Indexed]
        public string Album { get; set; }

        public string Genre { get; set; }
        public int TrackNumber { get; set; }
        public long DurationMs { get; set; }
        public long LastModifiedTicks { get; set; }
        public long LastScannedTicks { get; set; }
        public bool IsFavorite { get; set; }
    }
}