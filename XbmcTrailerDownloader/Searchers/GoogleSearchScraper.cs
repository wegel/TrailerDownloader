using System;
using System.Linq;
using HtmlAgilityPack;

namespace TrailerDownloader.Searchers
{
    public class GoogleSearchScraper : IGoogleSearch
    {
        public GoogleResult Search(string searchTerms, string referer = "wegel.ca")
        {
            var web = new HtmlWeb();
            web.UserAgent =
                "Mozilla/5.0 (iPhone; CPU iPhone OS 5_0 like Mac OS X) AppleWebKit/534.46 (KHTML, like Gecko) Version/5.1 Mobile/9A334 Safari/7534.48.3";
            web.PreRequest += request =>
                              {
                                  request.Referer = referer;
                                  return true;
                              };

            var resultsPage = web.Load(String.Format(
                "https://www.google.ca/search?q={0}&safe=off",
                searchTerms));

            var resultsContainer =
                resultsPage.DocumentNode.Descendants("div")
                    .Where(div => div.Attributes.Contains("class") && div.Attributes["class"].Value == "rc")
                    .ToList();

            var results = resultsContainer.Select(Convert).Where(r => r != null).ToList();

            return new GoogleResult() {responseData = new ResponseData {results = results}};

        }

        private static Result Convert(HtmlNode div)
        {
            var result = new Result();

            if (div.Descendants("h3").FirstOrDefault() == null)
                return null;

            var a = div.Descendants("h3").First().Descendants("a").FirstOrDefault();
            if (a == null || a.Attributes.Contains("href") == false)
                return null;

            result.title = a.InnerText;
            result.titleNoFormatting = a.InnerText;
            result.unescapedUrl = a.Attributes["href"].Value;

            return result;
        }

    }
}