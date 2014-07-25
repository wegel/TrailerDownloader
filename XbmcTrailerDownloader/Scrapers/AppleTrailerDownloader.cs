using System;
using System.Linq;
using System.Net;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using TrailerDownloader.Searchers;

namespace TrailerDownloader.Scrapers
{
    public class AppleTrailerScraper : ITrailerScraper
    {
        private readonly IGoogleSearch _searcher;

        public AppleTrailerScraper(IGoogleSearch searcher)
        {
            _searcher = searcher;
        }

        private string GetTrailerPageFromApple(Movie movie)
        {
            using (var web = new WebClient())
            {
                var json =
                    web.DownloadString(
                        string.Format("http://trailers.apple.com/trailers/home/scripts/quickfind.php?q={0}",
                            movie.Name));

                JObject o;
                if (string.IsNullOrEmpty(json) || (o = JObject.Parse(json)).HasValues == false || o.SelectToken("error") == null ||
                    o.SelectToken("error").ToString() != "False")
                {
                    Console.WriteLine("\tApple: not found (search step, json)");
                    return null;
                }

                JToken firstRelevantResult = null;
                var results = o.SelectToken("results");
                foreach (var r in results)
                {
                    var releaseDate = DateTime.Parse(r.SelectToken("releasedate").ToString());
                    if (releaseDate.Year == int.Parse(movie.Year))
                    {
                        firstRelevantResult = r;
                        break;
                    } 
                }

                if (firstRelevantResult == null)
                {
                    Console.WriteLine("\tApple: no relevant entry found in json");
                    return null;
                }



                return "http://trailers.apple.com/" + firstRelevantResult.SelectToken("location").ToString();
            }
        }

        private string GetTrailerPageFromWebSearch(Movie movie)
        {
            var searchTerm = "site:trailers.apple.com " + "\"" + movie.Name + "\"";


            var googleResult = _searcher.Search(searchTerm, "http://trailers.apple.com");

            var firstRelevantResult =
                googleResult.responseData.results.FirstOrDefault(
                    r => r.unescapedUrl.StartsWith("http://trailers.apple.com/trailers/"));

            if (firstRelevantResult == null)
            {
                searchTerm = "site:trailers.apple.com " +
                             string.Join(" ", movie.Name.Replace("&", "and").Split(' ').Select(s => "+" + s));

                googleResult = _searcher.Search(searchTerm, "http://trailers.apple.com");

                firstRelevantResult =
                    googleResult.responseData.results.FirstOrDefault(
                        r => r.unescapedUrl.StartsWith("trailers.apple.com/trailers/"));

                if (firstRelevantResult == null)
                {
                    Console.WriteLine("\tApple: not found");
                    return null;
                }
            }

            var titleFromGoogle =
                firstRelevantResult.titleNoFormatting.Split(new string[] {"- Movie Trailers -"}, StringSplitOptions.None)
                    .FirstOrDefault();

            if (string.IsNullOrEmpty(titleFromGoogle) ||
                (movie.NameWithoutPunctuation().DamerauLevenshteinDistanceTo(titleFromGoogle.Trim()) >
                 (movie.NameWithoutPunctuation().Length*0.20) &&
                 movie.NameWithoutSpacesAndStuff()
                     .DamerauLevenshteinDistanceTo(
                         firstRelevantResult.unescapedUrl.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
                             .Last()) > movie.NameWithoutSpacesAndStuff().Length*0.20))
            {
                Console.WriteLine("\tApple: not found (google result not relevant)");
                return null;
            }

            return firstRelevantResult.unescapedUrl;
        }

        private ScrapeResult ScrapeFromTrailerPage(string trailerPageUrl, Movie movie)
        {
            var trailersLink = trailerPageUrl + "/includes/playlists/itunes.inc";

            HtmlWeb hw = new HtmlWeb();
            var doc = hw.Load(trailersLink);

            var downloadLink = doc.DocumentNode.Descendants("a").FirstOrDefault(
                a =>
                    a.Attributes["href"] != null &&
                    a.Attributes["href"].Value.Contains("trailers.apple.com") &&
                    a.InnerText.ToLower().Contains("1080p")
                    && a.Attributes["href"].Value.Contains("-tlr")
                );

            if (downloadLink == null)
            {
                downloadLink = doc.DocumentNode.Descendants("a").FirstOrDefault(
                    a =>
                        a.Attributes["href"] != null &&
                        a.Attributes["href"].Value.Contains("trailers.apple.com") &&
                        a.InnerText.ToLower().Contains("720p")
                        && a.Attributes["href"].Value.Contains("-tlr")
                    );
            }

            if (downloadLink == null)
            {
                Console.WriteLine("\tApple: not found");
                return new ScrapeResult {Result = TrailerStatus.NoTrailerFound};
            }


            Console.WriteLine("\tApple: downloading");
            var link = downloadLink.Attributes["href"].Value;

            return new ScrapeResult {Result = TrailerStatus.Found, Url = link};
        }

        public ScrapeResult Scrape(Movie movie)
        {
            var trailerPageUrl = GetTrailerPageFromApple(movie);

            if (string.IsNullOrEmpty(trailerPageUrl))
            {
                return new ScrapeResult {Result = TrailerStatus.NoTrailerFound};
            }

            return ScrapeFromTrailerPage(trailerPageUrl, movie);
        }
    }
}