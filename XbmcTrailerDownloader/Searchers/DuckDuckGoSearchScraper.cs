using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace TrailerDownloader.Searchers
{
    public class DuckDuckGoSearchScraper : IGoogleSearch
    {
        public GoogleResult Search(string searchTerms, string referer)
        {
            var web = new HtmlWeb();

            web.PreRequest += request =>
                              {
                                  request.Referer = referer;
                                  return true;
                              };

            var resultsPage = web.Load(String.Format(
                "https://duckduckgo.com/?q={0}",
                searchTerms));

            var resultsContainer =
                resultsPage.DocumentNode.Descendants("div")
                    .FirstOrDefault(div => div.Id == "links");

            if (resultsContainer == null)
                return new GoogleResult() {responseData = new ResponseData() {results = new List<Result>()}};

            var links =
                resultsContainer.Descendants("a")
                    .Where(
                        a =>
                            a.Attributes.Contains("class") && a.Attributes.Contains("href") &&
                            a.Attributes["class"].Value == "large").ToList();

            

            var results = links.Select(Convert).Where(r => r != null).ToList();
            return new GoogleResult() { responseData = new ResponseData { results = results } };
        }

        private Result Convert(HtmlNode a)
        {
            var result = new Result();

            result.title = a.InnerText;
            result.titleNoFormatting = a.InnerText;
            result.unescapedUrl = a.Attributes["href"].Value;

            return result;
        }
    }
}