using System;
using System.Net;
using Newtonsoft.Json;

namespace TrailerDownloader.Searchers
{
    public class GoogleSearchByAPI : IGoogleSearch
    {
        public GoogleResult Search(string searchTerm, string referer = "wegel.ca")
        {
            using (var web = new WebClient())
            {
                web.Headers.Add("Referrer", "http://wegel.ca/");
                var webResult = web.DownloadString(String.Format(
                    "http://ajax.googleapis.com/ajax/services/search/web?v=1.0&q={0}&key=ABQIAAAAiUyTWB12tPVjqG4nW69ErxRMigmFFtNq-CNzqyKq0q3ttofZhBSBaxjc2bUCtQo0KKxUtxKgsCxLmQ",
                    searchTerm));

                return JsonConvert.DeserializeObject<GoogleResult>(webResult);
            }

        }

    }
}