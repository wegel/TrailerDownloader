namespace TrailerDownloader
{
    interface ITrailerScraper
    {
        ScrapeResult Scrape(Movie movie);
    }

    public class ScrapeResult
    {
        public TrailerStatus Result { get; set; }
        public string Url { get; set; }
    }

    public enum TrailerStatus
    {
        NoTrailerFound, FoundAndCompletelyDownloaded, DatabaseError, NetworkError,
        Found
    }
}
