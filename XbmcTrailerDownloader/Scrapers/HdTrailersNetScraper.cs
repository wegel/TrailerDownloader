using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using TrailerDownloader.Searchers;

namespace TrailerDownloader.Scrapers
{
    public class HdTrailersNetScraper : ITrailerScraper
    {
        private readonly IGoogleSearch _searcher;

        public HdTrailersNetScraper(IGoogleSearch searcher)
        {
            _searcher = searcher;
        }


        public ScrapeResult Scrape(Movie movie)
        {
            var searchTerm = "site:http://www.hd-trailers.net/ " + "\"" + movie.Name + "\"";


            var googleResult = _searcher.Search(searchTerm, "http://hd-trailers.net");

            var firstRelevantResult =
                googleResult.responseData.results.FirstOrDefault(r => r.unescapedUrl.StartsWith("http://www.hd-trailers.net/"));

            if (firstRelevantResult == null)
            {
                Console.WriteLine("\tHdTrailersNet: not found (step 1: no relevant Google result)");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }


            HtmlWeb hw = new HtmlWeb();
            var doc = hw.Load(firstRelevantResult.unescapedUrl);


            var movTrailerList =
                doc.DocumentNode.Descendants("a")
                    .Where(
                        a =>
                            a.Attributes.Contains("itemprop") && a.Attributes.Contains("href") &&
                            a.Attributes["itemprop"].Value == "trailer" && a.Attributes["href"].Value.EndsWith(".mov"))
                    .ToList();

            //In table
            if (movTrailerList.Any() == false)
            {
                movTrailerList =
                doc.DocumentNode.Descendants("a")
                    .Where(
                        a =>
                            a.ParentNode != null && a.ParentNode.ParentNode != null &&
                            a.ParentNode.ParentNode.Attributes.Contains("itemprop") &&
                            a.ParentNode.ParentNode.Attributes["itemprop"].Value == "trailer" &&
                            a.Attributes.Contains("href") &&
                            a.Attributes["href"].Value.EndsWith(".mov"))
                    .ToList();
            }

            var list = new List<string>();
            list.AddRange(movTrailerList.Where(a => a.InnerText.Contains("1080")).Select(a => a.Attributes["href"].Value));
            list.AddRange(movTrailerList.Where(a => a.InnerText.Contains("720")).Select(a => a.Attributes["href"].Value));


            var downloadLink = list.FirstOrDefault();
            if (string.IsNullOrEmpty(downloadLink))
            {
                Console.WriteLine("\tHdTrailersNet: (step 2: no suitable trailer links)");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }


            Console.WriteLine("\thd-trailers.net: downloading " + downloadLink);
            return new ScrapeResult { Result = TrailerStatus.Found, Url = downloadLink};
        }
    }
}