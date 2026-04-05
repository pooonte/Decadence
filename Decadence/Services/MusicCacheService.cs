using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using System.Runtime.Serialization.Json;
using System.Text;
using Decadence.Models;

namespace Decadence.Services
{
    public static class MusicCacheService
    {
        private static readonly string CacheFile = "music_cache.json";

        public static async Task SaveCacheAsync(List<CachedTrack> tracks)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<CachedTrack>));
                using (var stream = new MemoryStream())
                {
                    serializer.WriteObject(stream, tracks);
                    stream.Position = 0;
                    using (var reader = new StreamReader(stream))
                    {
                        var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                            CacheFile, CreationCollisionOption.ReplaceExisting);
                        await FileIO.WriteTextAsync(file, await reader.ReadToEndAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения: {ex.Message}");
            }
        }

        public static async Task<List<CachedTrack>> LoadCacheAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(CacheFile);
                string json = await FileIO.ReadTextAsync(file);

                var serializer = new DataContractJsonSerializer(typeof(List<CachedTrack>));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(stream) as List<CachedTrack> ?? new List<CachedTrack>();
                }
            }
            catch
            {
                return new List<CachedTrack>();
            }
        }

        public static async Task<bool> HasCacheAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(CacheFile);
                var props = await file.GetBasicPropertiesAsync();
                return props.Size > 0;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<List<CachedTrack>> FullScanAsync()
        {
            var tracks = await LibraryService.ScanLibraryAsync();
            await SaveCacheAsync(tracks);
            return tracks;
        }

        public static async Task<List<CachedTrack>> QuickCheckAsync(List<CachedTrack> existingCache)
        {
            if (existingCache == null || existingCache.Count == 0)
                return await FullScanAsync();

            var newTracks = await LibraryService.ScanLibraryAsync();

            if (newTracks.Count != existingCache.Count)
            {
                await SaveCacheAsync(newTracks);
                return newTracks;
            }

            return existingCache;
        }
    }
}