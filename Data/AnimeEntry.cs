using System;

namespace AnimeTracker.Data
{
    public enum AnimeStatus
    {
        WatchLater,
        Watching,
        Finished
    }

    public enum WatchLanguage
    {
        SubOnly,
        DubAvailable
    }

    public sealed class AnimeEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? FoundDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Rating { get; set; }
        public string Link { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public AnimeStatus Status { get; set; } = AnimeStatus.WatchLater;
        public WatchLanguage Language { get; set; } = WatchLanguage.SubOnly;
        public string Notes { get; set; } = string.Empty;
        public int Seasons { get; set; } = 1;
        public int EpisodesPerSeason { get; set; } = 12;
        public int EpisodesWatched { get; set; }

        public int TotalEpisodes => Seasons * EpisodesPerSeason;

        public string ProgressLabel => TotalEpisodes == 0
            ? "0/0"
            : $"{EpisodesWatched}/{TotalEpisodes}";

        public int ProgressPercent => TotalEpisodes == 0
            ? 0
            : (int)Math.Round(100.0 * EpisodesWatched / TotalEpisodes);

        public string StatusLabel => Status switch
        {
            AnimeStatus.WatchLater => "Watch Later",
            AnimeStatus.Watching => "Watching",
            AnimeStatus.Finished => "Finished",
            _ => "Unknown"
        };

        public string LanguageLabel => Language switch
        {
            WatchLanguage.SubOnly => "Sub Only",
            WatchLanguage.DubAvailable => "Dub Available",
            _ => "Sub"
        };
    }
}
