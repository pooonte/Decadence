using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using Windows.Storage;
using Decadence.Models;
using System.Threading;

namespace Decadence.Services
{
    public static class LibraryDatabase
    {
        private static SQLiteAsyncConnection _db;
        private static readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        public static async Task<SQLiteAsyncConnection> GetConnectionAsync()
        {
            if (_db != null) return _db;

            await _initLock.WaitAsync();
            try
            {
                if (_db == null)
                {
                    string dbPath = System.IO.Path.Combine(
                        ApplicationData.Current.LocalFolder.Path, "library.db3");

                    _db = new SQLiteAsyncConnection(dbPath);

                    await _db.CreateTableAsync<TrackRecord>();
                    await _db.CreateTableAsync<PlaylistRecord>();
                    await _db.CreateTableAsync<PlaylistTrackRecord>();
                }
            }
            finally
            {
                _initLock.Release();
            }

            return _db;
        }

        // ===== Tracks =====

        public static async Task<List<TrackRecord>> GetAllTracksAsync()
        {
            var db = await GetConnectionAsync();
            return await db.Table<TrackRecord>().OrderBy(t => t.Title).ToListAsync();
        }

        public static async Task<Dictionary<string, TrackRecord>> GetAllTracksByPathAsync()
        {
            var all = await GetAllTracksAsync();
            return all.ToDictionary(t => t.FilePath, t => t);
        }

        public static async Task UpsertTracksAsync(List<TrackRecord> tracks)
        {
            var db = await GetConnectionAsync();
            await db.RunInTransactionAsync(conn =>
            {
                foreach (var track in tracks)
                {
                    var existing = conn.Table<TrackRecord>()
                        .FirstOrDefault(t => t.FilePath == track.FilePath);

                    if (existing != null)
                    {
                        track.Id = existing.Id;
                        track.IsFavorite = existing.IsFavorite; // не затираем избранное при пересканировании
                        conn.Update(track);
                    }
                    else
                    {
                        conn.Insert(track);
                    }
                }
            });
        }

        public static async Task DeleteTracksByPathAsync(IEnumerable<string> filePaths)
        {
            var db = await GetConnectionAsync();
            var paths = filePaths.ToList();
            if (paths.Count == 0) return;

            await db.RunInTransactionAsync(conn =>
            {
                foreach (var path in paths)
                    conn.Table<TrackRecord>().Delete(t => t.FilePath == path);
            });
        }

        public static async Task SetFavoriteAsync(int trackId, bool isFavorite)
        {
            var db = await GetConnectionAsync();
            var track = await db.Table<TrackRecord>().FirstOrDefaultAsync(t => t.Id == trackId);
            if (track == null) return;
            track.IsFavorite = isFavorite;
            await db.UpdateAsync(track);
        }

        public static async Task<bool> ToggleFavoriteAsync(int trackId)
        {
            var db = await GetConnectionAsync();
            var track = await db.Table<TrackRecord>().FirstOrDefaultAsync(t => t.Id == trackId);
            if (track == null) return false;

            track.IsFavorite = !track.IsFavorite;
            await db.UpdateAsync(track);
            return track.IsFavorite;
        }

        public static async Task<List<TrackRecord>> SearchTracksAsync(string queryLower)
        {
            var db = await GetConnectionAsync();
            return await db.Table<TrackRecord>()
                .Where(t => t.TitleLower.Contains(queryLower)
                         || t.ArtistLower.Contains(queryLower)
                         || t.AlbumLower.Contains(queryLower))
                .ToListAsync();
        }

        // ===== Playlists =====

        public static async Task<List<PlaylistRecord>> GetPlaylistsAsync()
        {
            var db = await GetConnectionAsync();
            return await db.Table<PlaylistRecord>().ToListAsync();
        }

        public static async Task<int> CreatePlaylistAsync(string name)
        {
            var db = await GetConnectionAsync();
            var playlist = new PlaylistRecord { Name = name };
            await db.InsertAsync(playlist);
            return playlist.Id;
        }
        public static async Task UpdatePlaylistAsync(int playlistId, string name, string coverPath)
        {
            var db = await GetConnectionAsync();
            var playlist = await db.Table<PlaylistRecord>().FirstOrDefaultAsync(p => p.Id == playlistId);
            if (playlist == null) return;
            playlist.Name = name;
            playlist.CoverPath = coverPath;
            await db.UpdateAsync(playlist);
        }
        public static async Task DeletePlaylistAsync(int playlistId)
        {
            var db = await GetConnectionAsync();
            await db.RunInTransactionAsync(conn =>
            {
                conn.Table<PlaylistTrackRecord>().Delete(pt => pt.PlaylistId == playlistId);
                conn.Table<PlaylistRecord>().Delete(p => p.Id == playlistId);
            });
        }

        public static async Task<List<TrackRecord>> GetPlaylistTracksAsync(int playlistId)
        {
            var db = await GetConnectionAsync();
            var links = await db.Table<PlaylistTrackRecord>()
                .Where(pt => pt.PlaylistId == playlistId)
                .OrderBy(pt => pt.Position)
                .ToListAsync();

            var trackIds = new HashSet<int>(links.Select(l => l.TrackId));
            var allTracks = await db.Table<TrackRecord>().ToListAsync();
            var byId = allTracks.Where(t => trackIds.Contains(t.Id)).ToDictionary(t => t.Id);

            return links.Where(l => byId.ContainsKey(l.TrackId))
                         .Select(l => byId[l.TrackId])
                         .ToList();
        }

        public static async Task AddTrackToPlaylistAsync(int playlistId, int trackId)
        {
            var db = await GetConnectionAsync();
            bool exists = await db.Table<PlaylistTrackRecord>()
                .Where(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId)
                .CountAsync() > 0;
            if (exists) return;

            int maxPos = (await db.Table<PlaylistTrackRecord>()
                .Where(pt => pt.PlaylistId == playlistId)
                .ToListAsync())
                .Select(pt => (int?)pt.Position).Max() ?? -1;

            await db.InsertAsync(new PlaylistTrackRecord
            {
                PlaylistId = playlistId,
                TrackId = trackId,
                Position = maxPos + 1
            });
        }

        public static async Task RemoveTrackFromPlaylistAsync(int playlistId, int trackId)
        {
            var db = await GetConnectionAsync();
            await db.Table<PlaylistTrackRecord>()
                .Where(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId)
                .DeleteAsync();
        }

        public static async Task<int> GetTrackCountAsync()
        {
            var db = await GetConnectionAsync();
            return await db.Table<TrackRecord>().CountAsync();
        }

        public static async Task ReorderPlaylistTracksAsync(int playlistId, List<int> orderedTrackIds)
        {
            var db = await GetConnectionAsync();
            await db.RunInTransactionAsync(conn =>
            {
                for (int i = 0; i < orderedTrackIds.Count; i++)
                {
                    conn.Execute(
                        "UPDATE PlaylistTracks SET Position = ? WHERE PlaylistId = ? AND TrackId = ?",
                        i, playlistId, orderedTrackIds[i]);
                }
            });
        }
    }
}