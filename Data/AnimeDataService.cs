using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace AnimeTracker.Data
{
    public sealed class AnimeDataService
    {
        private const string LocalStorageKey = "anime-tracker-entries";
        private readonly IJSRuntime _jsRuntime;
        private bool _initialized;

        public List<AnimeEntry> Entries { get; private set; } = new();

        public AnimeDataService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            var json = await _jsRuntime.InvokeAsync<string?>("animeTracker.getLocalData", LocalStorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    Entries = JsonSerializer.Deserialize<List<AnimeEntry>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    }) ?? new List<AnimeEntry>();
                }
                catch
                {
                    Entries = new List<AnimeEntry>();
                }
            }

        }

        public Task SaveEntriesAsync()
        {
            var snapshot = JsonSerializer.Serialize(Entries, new JsonSerializerOptions
            {
                WriteIndented = false,
            });

            return _jsRuntime.InvokeVoidAsync("animeTracker.setLocalData", LocalStorageKey, snapshot).AsTask();
        }

        public List<AnimeEntry> GetByStatus(AnimeStatus status)
        {
            return Entries
                .Where(entry => entry.Status == status)
                .OrderBy(entry => entry.Title)
                .ToList();
        }

        public AnimeEntry? GetById(string id)
        {
            return Entries.FirstOrDefault(entry => entry.Id == id);
        }

        public async Task AddOrUpdateAsync(AnimeEntry entry)
        {
            var existing = GetById(entry.Id);
            if (existing is null)
            {
                Entries.Add(entry);
            }
            else
            {
                var index = Entries.IndexOf(existing);
                Entries[index] = entry;
            }

            await SaveEntriesAsync();
        }

        public async Task RemoveAsync(string id)
        {
            var entry = GetById(id);
            if (entry is null)
            {
                return;
            }

            Entries.Remove(entry);
            await SaveEntriesAsync();
        }

        public async Task MoveStatusAsync(string id, AnimeStatus status)
        {
            var entry = GetById(id);
            if (entry is null)
            {
                return;
            }

            entry.Status = status;
            if (status == AnimeStatus.Finished)
            {
                entry.EndDate ??= DateTime.Today;
            }
            await SaveEntriesAsync();
        }

        public async Task AdjustProgressAsync(string id, int delta)
        {
            var entry = GetById(id);
            if (entry is null)
            {
                return;
            }

            entry.EpisodesWatched = Math.Max(0, Math.Min(entry.TotalEpisodes, entry.EpisodesWatched + delta));
            await SaveEntriesAsync();
        }

        public async Task SetProgressAsync(string id, int value)
        {
            var entry = GetById(id);
            if (entry is null)
            {
                return;
            }

            entry.EpisodesWatched = Math.Max(0, Math.Min(entry.TotalEpisodes, value));
            await SaveEntriesAsync();
        }

        public async Task ExportCsvAsync()
        {
            var csv = BuildCsv();
            var fileName = "AnimeTrackerList.csv";
            await _jsRuntime.InvokeVoidAsync("animeTracker.saveTextFile", fileName, csv);
        }

        public Task<string> GetExportCsvTextAsync()
        {
            return Task.FromResult(BuildCsv());
        }

        public async Task ImportCsvTextAsync(string csvText)
        {
            var imported = ParseCsv(csvText).ToList();
            if (imported.Count == 0)
            {
                return;
            }

            Entries.RemoveAll(entry => imported.Any(import => !string.IsNullOrWhiteSpace(import.Title) && entry.Title.Equals(import.Title, StringComparison.OrdinalIgnoreCase) && entry.Link == import.Link));
            Entries.AddRange(imported);
            await SaveEntriesAsync();
        }

        public async Task ImportCsvAsync(IBrowserFile file)
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 5_000_000);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            await ImportCsvTextAsync(text);
        }

        public async Task ClearAllAsync()
        {
            Entries.Clear();
            await SaveEntriesAsync();
        }

        private string BuildCsv()
        {
            static string Escape(string? input)
            {
                if (string.IsNullOrEmpty(input))
                {
                    return string.Empty;
                }

                if (input.Contains('"') || input.Contains(',') || input.Contains('\n') || input.Contains('\r'))
                {
                    return "\"" + input.Replace("\"", "\"\"") + "\"";
                }

                return input;
            }

            var header = new[]
            {
                "Id",
                "Title",
                "Description",
                "Rating",
                "FoundDate",
                "StartDate",
                "EndDate",
                "Link",
                "ImageUrl",
                "Genre",
                "Status",
                "Language",
                "Seasons",
                "EpisodesPerSeason",
                "EpisodesWatched",
                "Notes"
            };

            var lines = new List<string> { string.Join(',', header) };
            foreach (var entry in Entries)
            {
                lines.Add(string.Join(',', new[]
                {
                    Escape(entry.Id),
                    Escape(entry.Title),
                    Escape(entry.Description),
                    entry.Rating.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    entry.FoundDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    entry.StartDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    entry.EndDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    Escape(entry.Link),
                    Escape(entry.ImageUrl),
                    Escape(entry.Genre),
                    entry.Status.ToString(),
                    entry.Language.ToString(),
                    entry.Seasons.ToString(),
                    entry.EpisodesPerSeason.ToString(),
                    entry.EpisodesWatched.ToString(),
                    Escape(entry.Notes)
                }));
            }

            return string.Join("\n", lines);
        }

        private IEnumerable<AnimeEntry> ParseCsv(string csvText)
        {
            var rows = ReadRows(csvText).ToList();
            if (rows.Count < 2)
            {
                return Enumerable.Empty<AnimeEntry>();
            }

            var header = rows[0];
            var columns = header.Select((value, index) => (name: value.Trim(), index))
                .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

            var entries = new List<AnimeEntry>();
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row.Count == 0 || string.IsNullOrWhiteSpace(string.Join(string.Empty, row)))
                {
                    continue;
                }

                var entry = new AnimeEntry
                {
                    Id = GetValue(columns, row, "Id") ?? Guid.NewGuid().ToString("N"),
                    Title = GetValue(columns, row, "Title") ?? string.Empty,
                    Description = GetValue(columns, row, "Description") ?? string.Empty,
                    Rating = ParseDouble(GetValue(columns, row, "Rating"), 0),
                    FoundDate = ParseDate(GetValue(columns, row, "FoundDate")),
                    StartDate = ParseDate(GetValue(columns, row, "StartDate")),
                    EndDate = ParseDate(GetValue(columns, row, "EndDate")),
                    Link = GetValue(columns, row, "Link") ?? string.Empty,
                    ImageUrl = GetValue(columns, row, "ImageUrl") ?? string.Empty,
                    Genre = GetValue(columns, row, "Genre") ?? string.Empty,
                    Status = ParseEnum(GetValue(columns, row, "Status"), AnimeStatus.WatchLater),
                    Language = ParseEnum(GetValue(columns, row, "Language"), WatchLanguage.SubOnly),
                    Seasons = ParseInt(GetValue(columns, row, "Seasons"), 1),
                    EpisodesPerSeason = ParseInt(GetValue(columns, row, "EpisodesPerSeason"), 1),
                    EpisodesWatched = ParseInt(GetValue(columns, row, "EpisodesWatched"), 0),
                    Notes = GetValue(columns, row, "Notes") ?? string.Empty,
                };

                if (string.IsNullOrWhiteSpace(entry.Title))
                {
                    continue;
                }

                if (entry.EpisodesWatched > entry.TotalEpisodes)
                {
                    entry.EpisodesWatched = entry.TotalEpisodes;
                }

                entries.Add(entry);
            }

            return entries;
        }

        private static List<List<string>> ReadRows(string csvText)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var i = 0;

            while (i < csvText.Length)
            {
                var c = csvText[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }

                        inQuotes = false;
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentRow.Add(field.ToString());
                        field.Clear();
                    }
                    else if (c == '\r')
                    {
                        // ignore
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(field.ToString());
                        field.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                    }
                    else
                    {
                        field.Append(c);
                    }
                }

                i++;
            }

            if (field.Length > 0 || inQuotes || currentRow.Count > 0)
            {
                currentRow.Add(field.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }

        private static string? GetValue(Dictionary<string, int> columns, List<string> row, string columnName)
        {
            return columns.TryGetValue(columnName, out var index) && index < row.Count
                ? row[index]
                : null;
        }

        private static int ParseInt(string? value, int defaultValue)
        {
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        private static double ParseDouble(string? value, double defaultValue)
        {
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
        }

        private static DateTime? ParseDate(string? value)
        {
            if (DateTime.TryParse(value, out var date))
            {
                return date.Date;
            }

            return null;
        }

        private static T ParseEnum<T>(string? value, T defaultValue) where T : struct
        {
            return Enum.TryParse(value, ignoreCase: true, out T parsed) ? parsed : defaultValue;
        }

    }
}
