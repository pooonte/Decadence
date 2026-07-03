using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Decadence.Models;

namespace Decadence.Services
{
    public static class PlaylistStorage
    {
        public static async Task<List<Playlist>> LoadPlaylistsAsync()
        {
            var records = await LibraryDatabase.GetPlaylistsAsync();
            var result = new List<Playlist>();

            foreach (var record in records)
            {
                var trackRecords = await LibraryDatabase.GetPlaylistTracksAsync(record.Id);
                result.Add(new Playlist
                {
                    Id = record.Id,
                    Name = record.Name,
                    CoverPath = record.CoverPath,
                    Tracks = trackRecords.Select(ToTrackItem).ToList()
                });
            }

            return result;
        }

        public static async Task SavePlaylistsAsync(List<Playlist> playlists)
        {
            var existingRecords = await LibraryDatabase.GetPlaylistsAsync();
            var keptIds = new List<int>();

            foreach (var playlist in playlists)
            {
                int playlistId;
                if (playlist.Id == 0)
                {
                    playlistId = await LibraryDatabase.CreatePlaylistAsync(playlist.Name);
                    playlist.Id = playlistId;
                }
                else
                {
                    playlistId = playlist.Id;
                    await LibraryDatabase.UpdatePlaylistAsync(playlistId, playlist.Name, playlist.CoverPath);
                }
                keptIds.Add(playlistId);

                var currentTrackRecords = await LibraryDatabase.GetPlaylistTracksAsync(playlistId);
                var currentTrackIds = new HashSet<int>(currentTrackRecords.Select(t => t.Id));
                var desiredTrackIds = new HashSet<int>(playlist.Tracks.Select(t => t.Id));

                foreach (var id in desiredTrackIds.Except(currentTrackIds))
                    await LibraryDatabase.AddTrackToPlaylistAsync(playlistId, id);

                foreach (var id in currentTrackIds.Except(desiredTrackIds))
                    await LibraryDatabase.RemoveTrackFromPlaylistAsync(playlistId, id);
            }

            // Плейлисты, которых больше нет в переданном списке — считаем удалёнными
            var removedIds = existingRecords.Select(p => p.Id).Except(keptIds).ToList();
            foreach (var id in removedIds)
                await LibraryDatabase.DeletePlaylistAsync(id);
        }

        private static TrackItem ToTrackItem(TrackRecord r) => new TrackItem
        {
            Id = r.Id,
            FilePath = r.FilePath,
            Title = r.Title,
            Artist = r.Artist,
            Album = r.Album,
            Genre = r.Genre,
            TrackNumber = r.TrackNumber,
            Duration = TimeSpan.FromMilliseconds(r.DurationMs)
        };
    }
}