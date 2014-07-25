using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using TrailerDownloader.Searchers;

namespace TrailerDownloader.Scrapers
{
    //Not Functional
    public class YahooTrailerScraper: ITrailerScraper
    {
        public ScrapeResult Scrape(Movie movie)
        {
            var searchTerm = "site:http://movies.yahoo.com " + "\"" + movie.Name + "\" -\"ca.\"";

            var googleResult = (new GoogleSearchScraper()).Search(searchTerm);

            var firstRelevantResult =
                googleResult.responseData.results.FirstOrDefault(r => r.unescapedUrl.StartsWith("http://movies.yahoo.com/movie"));

            if (firstRelevantResult == null)
            {
                Console.WriteLine("\tYahoo: not found (step 1: no relevant Google result)");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }

            var web = new HtmlWeb();
            var doc = web.Load(firstRelevantResult.unescapedUrl);

            var trailerPageLink =
                doc.DocumentNode.Descendants("div")
                    .Where(div => div.Id == "mediamovietrailersgs")
                    .Select(
                        div =>
                            div.Descendants("a")
                                .Where(
                                    a =>
                                        a.Attributes.Contains("href") && a.Attributes["href"].Value.StartsWith("/video")))
                    .FirstOrDefault();

            if (trailerPageLink == null)
            {
                Console.WriteLine("\tYahoo: not found (step 2: can't find trailers page)");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }

            

            Console.WriteLine("\tYahoo: not found");
            return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
        }
    }
}
