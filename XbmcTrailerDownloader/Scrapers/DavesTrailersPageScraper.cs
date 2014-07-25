using System;
using System.Linq;
using System.Net;
using HtmlAgilityPack;

namespace TrailerDownloader.Scrapers
{
    public class DavesTrailersPageScraper : ITrailerScraper
    {
        public ScrapeResult Scrape(Movie movie)
        {
            HtmlDocument doc;

            while (true)
            {
                try
                {
                    HtmlWeb hw = new HtmlWeb();
                    doc = hw.Load(string.Format(@"http://www.davestrailerpage.co.uk/trailers_{0}.html", movie.Name.ToLowerInvariant()[0]));
                    break;
                }
                catch (WebException wex)
                {
                    Console.WriteLine("Got timeout or other: " + wex.ToString());
                }
            }



            if (!doc.DocumentNode.InnerHtml.ToLowerInvariant().Contains(movie.Name.ToLowerInvariant()))
            {
                Console.WriteLine("\tDave's trailer page: not found");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }


            var titleNode = doc.DocumentNode.Descendants("b").Where(n => n.InnerText.ToLowerInvariant() == movie.Name.ToLowerInvariant())
                .FirstOrDefault();
            if (titleNode == null)
            {
                Console.WriteLine("\tDave's trailer page: not found");
                return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
            }


            try
            {
                foreach (var trailerNode in titleNode.ParentNode.ChildNodes.Where(n => n.Name == "ul").First().Descendants("b").Where(n => n.InnerHtml.ToLowerInvariant().Contains("trailer")))
                {
                    var link = trailerNode.ParentNode.Descendants("a").Where(n => n.InnerText == "720P").First().Attributes["href"].Value;

                    Console.WriteLine("\tDave's trailer page: downloading");
                    return new ScrapeResult {Result = TrailerStatus.Found, Url = link};
                }
            }
            catch (InvalidOperationException ioex)
            {
            }

            Console.WriteLine("\tDave's trailer page: not found");
            return new ScrapeResult { Result = TrailerStatus.NoTrailerFound };
        }
    }
}
