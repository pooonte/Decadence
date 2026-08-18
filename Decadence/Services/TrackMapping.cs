using System;
using Decadence.Models;

namespace Decadence.Services
{
    public static class TrackMapping
    {
        public static TrackItem ToTrackItem(this TrackRecord r) => new TrackItem
        {
            Id = r.Id,
            FilePath = r.FilePath,
            Title = r.Title,
            Artist = r.Artist,
            Album = r.Album,
            Genre = r.Genre,
            TrackNumber = r.TrackNumber,
            IsFavorite = r.IsFavorite,
            Duration = TimeSpan.FromMilliseconds(r.DurationMs)
        };
    }
}