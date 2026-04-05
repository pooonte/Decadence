using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Decadence.Models;

namespace Decadence.Services
{
    public static class LibraryService
    {
        public static async Task<List<CachedTrack>> ScanLibraryAsync()
        {
            var tracks = new List<CachedTrack>();

            try
            {
                var musicFolder = KnownFolders.MusicLibrary;
                var queryOptions = new QueryOptions
                {
                    FolderDepth = FolderDepth.Deep,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable
                };

                queryOptions.FileTypeFilter.Add(".mp3");
                queryOptions.FileTypeFilter.Add(".flac");
                queryOptions.FileTypeFilter.Add(".m4a");
                queryOptions.FileTypeFilter.Add(".wma");
                queryOptions.FileTypeFilter.Add(".wav");

                var query = musicFolder.CreateFileQueryWithOptions(queryOptions);
                var files = await query.GetFilesAsync();

                foreach (var file in files)
                {
                    try
                    {
                        var props = await file.Properties.GetMusicPropertiesAsync();
                        var basicProps = await file.GetBasicPropertiesAsync();

                        tracks.Add(new CachedTrack
                        {
                            FilePath = file.Path,
                            Title = string.IsNullOrEmpty(props.Title) ? file.DisplayName : props.Title,
                            Artist = string.IsNullOrEmpty(props.Artist) ? "Неизвестный исполнитель" : props.Artist,
                            Album = string.IsNullOrEmpty(props.Album) ? "Неизвестный альбом" : props.Album,
                            Duration = props.Duration.ToString(),
                            LastModified = basicProps.DateModified.DateTime,
                            LastScanned = DateTime.Now
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сканирования: {ex.Message}");
            }

            return tracks;
        }

        public static void UpdateCollections(List<CachedTrack> cachedTracks,
            ObservableCollection<TrackItem> tracks,
            ObservableCollection<ArtistItem> artists,
            ObservableCollection<AlbumItem> albums)
        {
            tracks.Clear();
            artists.Clear();
            albums.Clear();

            var artistDict = new Dictionary<string, List<TrackItem>>();
            var albumDict = new Dictionary<string, List<TrackItem>>();

            foreach (var cached in cachedTracks)
            {
                var track = new TrackItem
                {
                    FilePath = cached.FilePath,
                    Title = cached.Title,
                    Artist = cached.Artist,
                    Album = cached.Album,
                    Duration = TimeSpan.Parse(cached.Duration)
                };

                tracks.Add(track);

                if (!artistDict.ContainsKey(track.Artist))
                    artistDict[track.Artist] = new List<TrackItem>();
                artistDict[track.Artist].Add(track);

                if (!albumDict.ContainsKey(track.Album))
                    albumDict[track.Album] = new List<TrackItem>();
                albumDict[track.Album].Add(track);
            }

            foreach (var kvp in artistDict.OrderBy(k => k.Key))
            {
                artists.Add(new ArtistItem
                {
                    Name = kvp.Key,
                    FirstTrack = kvp.Value.First(),
                    TrackCount = kvp.Value.Count
                });
            }

            foreach (var kvp in albumDict
                .Where(k => k.Key != "Неизвестный альбом")
                .OrderBy(k => k.Key))
            {
                albums.Add(new AlbumItem
                {
                    Name = kvp.Key,
                    Artist = kvp.Value.First().Artist,
                    FirstTrack = kvp.Value.First(),
                    TrackCount = kvp.Value.Count
                });
            }
        }
    }
}