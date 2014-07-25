using System;
using System.Linq;
using System.Threading;
using TrailerDownloader.Searchers;

namespace TrailerDownloader.Scrapers
{
    public class YoutubeVideoScraper: ITrailerScraper
    {
        private readonly IGoogleSearch _searcher;

        public YoutubeVideoScraper(IGoogleSearch searcher)
        {
            _searcher = searcher;
        }

        public ScrapeResult Scrape(Movie movie)
        {
            var searchTerm = "site:youtube.com " + "\"" + movie.Name + "\" trailer";


            var googleResult = _searcher.Search(searchTerm, "http://www.youtube.com");

            var firstRelevantResult =
                googleResult.responseData.results.FirstOrDefault(r => r.unescapedUrl.Contains("youtube.com/watch"));

            if (firstRelevantResult == null)
            {
                Console.WriteLine("\tYoutube: not found");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }

            var youtubeUrls = YouTubeDownloader.GetYouTubeVideoUrls(firstRelevantResult.unescapedUrl.Replace("m.youtube.com", "www.youtube.com"));

            if (youtubeUrls.Any() == false)
            {
                Thread.Sleep(20);
                youtubeUrls = YouTubeDownloader.GetYouTubeVideoUrls(firstRelevantResult.unescapedUrl);
                if (youtubeUrls.Any() == false)
                {
                    Console.WriteLine("\tYoutube: not found");
                    return new ScrapeResult {Result = TrailerStatus.NoTrailerFound};
                }
            }

            var mostQualityUrl = youtubeUrls.Where(y => y.Extention == "mp4" && y.Dimension.Width < 2000)
                .OrderByDescending(y => y.Dimension.Width)
                .FirstOrDefault();

            if (mostQualityUrl == null)
            {
                Console.WriteLine("\tYoutube: not found");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }

            Console.Write("\tYoutube: downloading " + mostQualityUrl.DownloadUrl);

            return new ScrapeResult { Result = TrailerStatus.Found, Url = mostQualityUrl.DownloadUrl };
        }
    }
}
