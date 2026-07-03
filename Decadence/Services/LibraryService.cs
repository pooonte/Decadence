using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Decadence.Models;

namespace Decadence.Services
{
    public static class LibraryService
    {
        private static readonly string[] SupportedExtensions =
            { ".mp3", ".flac", ".m4a", ".wma", ".wav" };

        // Полное сканирование: читает теги ВСЕХ файлов и синхронизирует с базой.
        // Используется при первом запуске и при ручном "Обновить библиотеку".
        public static async Task<int> FullScanAsync()
        {
            var files = await GetMusicFilesAsync();
            var existingByPath = await LibraryDatabase.GetAllTracksByPathAsync();

            var toUpsert = new List<TrackRecord>();
            var seenPaths = new HashSet<string>();

            foreach (var file in files)
            {
                seenPaths.Add(file.Path);
                try
                {
                    var record = await BuildTrackRecordAsync(file);
                    toUpsert.Add(record);
                }
                catch { /* повреждённый/недоступный файл — пропускаем */ }
            }

            if (toUpsert.Count > 0)
                await LibraryDatabase.UpsertTracksAsync(toUpsert);

            // Удаляем из базы то, чего больше нет на диске
            var removedPaths = existingByPath.Keys.Where(p => !seenPaths.Contains(p)).ToList();
            if (removedPaths.Count > 0)
                await LibraryDatabase.DeleteTracksByPathAsync(removedPaths);

            return toUpsert.Count;
        }

        // Лёгкая проверка: сравнивает только путь + дату изменения, БЕЗ чтения тегов.
        // Если что-то реально изменилось — дочитывает теги только для изменившихся файлов.
        public static async Task<bool> QuickCheckAsync()
        {
            var files = await GetMusicFilesAsync();
            var existingByPath = await LibraryDatabase.GetAllTracksByPathAsync();

            var toUpsert = new List<TrackRecord>();
            var seenPaths = new HashSet<string>();
            bool anyChange = false;

            foreach (var file in files)
            {
                seenPaths.Add(file.Path);

                Windows.Storage.FileProperties.BasicProperties basicProps;
                try { basicProps = await file.GetBasicPropertiesAsync(); }
                catch { continue; }

                long modifiedTicks = basicProps.DateModified.DateTime.Ticks;

                bool isNew = !existingByPath.TryGetValue(file.Path, out var existing);
                bool isChanged = !isNew && existing.LastModifiedTicks != modifiedTicks;

                if (isNew || isChanged)
                {
                    anyChange = true;
                    try
                    {
                        var record = await BuildTrackRecordAsync(file);
                        toUpsert.Add(record);
                    }
                    catch { }
                }
            }

            if (toUpsert.Count > 0)
                await LibraryDatabase.UpsertTracksAsync(toUpsert);

            var removedPaths = existingByPath.Keys.Where(p => !seenPaths.Contains(p)).ToList();
            if (removedPaths.Count > 0)
            {
                await LibraryDatabase.DeleteTracksByPathAsync(removedPaths);
                anyChange = true;
            }

            return anyChange;
        }

        private static async Task<IReadOnlyList<StorageFile>> GetMusicFilesAsync()
        {
            var musicFolder = KnownFolders.MusicLibrary;
            var queryOptions = new QueryOptions
            {
                FolderDepth = FolderDepth.Deep,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };
            foreach (var ext in SupportedExtensions)
                queryOptions.FileTypeFilter.Add(ext);

            var query = musicFolder.CreateFileQueryWithOptions(queryOptions);
            return await query.GetFilesAsync();
        }

        private static async Task<TrackRecord> BuildTrackRecordAsync(StorageFile file)
        {
            var props = await file.Properties.GetMusicPropertiesAsync();
            var basicProps = await file.GetBasicPropertiesAsync();

            return new TrackRecord
            {
                FilePath = file.Path,
                Title = string.IsNullOrEmpty(props.Title) ? file.DisplayName : props.Title,
                Artist = string.IsNullOrEmpty(props.Artist) ? "Неизвестный исполнитель" : props.Artist,
                Album = string.IsNullOrEmpty(props.Album) ? "Неизвестный альбом" : props.Album,
                Genre = props.Genre?.FirstOrDefault() ?? "",
                TrackNumber = (int)props.TrackNumber,
                DurationMs = (long)props.Duration.TotalMilliseconds,
                LastModifiedTicks = basicProps.DateModified.DateTime.Ticks,
                LastScannedTicks = DateTime.Now.Ticks
            };
        }
    }
}