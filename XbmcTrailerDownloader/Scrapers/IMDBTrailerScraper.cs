using System;
using System.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace TrailerDownloader.Scrapers
{
    public class IMDBTrailerScraper: ITrailerScraper
    {

        public ScrapeResult Scrape(Movie movie)
        {
            var imdbSource = String.Format(
                "http://www.imdb.com/title/{0}/",
                movie.ImdbId);

            var hw = new HtmlWeb();
            var doc = hw.Load(imdbSource);

            var imdbviLink = doc.DocumentNode.Descendants("a").FirstOrDefault(a => a.Attributes["itemprop"] != null && a.Attributes["itemprop"].Value == "trailer");

            if (imdbviLink == null || imdbviLink.Attributes["data-video"] == null)
            {
                Console.WriteLine("\tIMDB: not found (step1)");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }

            imdbSource =
                string.Format("http://www.imdb.com/video/imdb/{0}/imdbvideo?format=720p&type=single&track_present=0&const={1}", imdbviLink.Attributes["data-video"].Value, movie.ImdbId);
            doc = hw.Load(imdbSource);

            var jsonContainer = doc.DocumentNode.Descendants("script").FirstOrDefault(s => s.Attributes["class"].Value == "imdb-player-data");
            if (jsonContainer == null)
            {
                Console.WriteLine("\tIMDB: not found (step1)");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }

            var json = jsonContainer.InnerHtml;
            JObject o = JObject.Parse(json);

            var link = o.SelectToken("videoPlayerObject.video.url").ToString();
            if (link.StartsWith("http") == false)
            {
                Console.WriteLine("\tIMDB: not found (not an http link)");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }

            Console.WriteLine("\tIMDB: downloading " + link);
            return new ScrapeResult {Result = TrailerStatus.Found, Url = link};

        }
    }
}