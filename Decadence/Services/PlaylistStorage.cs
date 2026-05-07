using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Decadence.Models;

namespace Decadence.Services
{
    public static class PlaylistStorage
    {
        private static readonly string FileName = "playlists.json";

        public static async Task SavePlaylistsAsync(List<Playlist> playlists)
        {
            try
            {
                string json = JsonConvert.SerializeObject(playlists, Formatting.Indented);
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения плейлистов: {ex.Message}");
            }
        }

        public static async Task<List<Playlist>> LoadPlaylistsAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(FileName);
                string json = await FileIO.ReadTextAsync(file);
                return JsonConvert.DeserializeObject<List<Playlist>>(json) ?? new List<Playlist>();
            }
            catch
            {
                return new List<Playlist>();
            }
        }
    }
}