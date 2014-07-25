using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using HtmlAgilityPack;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ninject;
using TrailerDownloader.Scrapers;
using TrailerDownloader.Searchers;

namespace TrailerDownloader
{
    class Program
    {
        //private string _moviesDirectoryPath = @"\\wegel-nas\file_sharing\movies";
        private string _connectionString = "SERVER=192.168.3.4;PORT=3306;DATABASE=MyVideos75;UID=xbmc;PASSWORD=xbmc;";
        private string _movieCollectionBasePath = "/storage/mount/file_sharing/movies/";
        private List<ITrailerScraper> _scrapers;

        static void Main(string[] args)
        {
            Bootstrapper.Bootstrap();

            var p = new Program();
            if(args.Any(a => a == "-s" || a == "--single"))
                p.DownloadSingleTrailer(args.Where(a => a != "-s" && a != "--single").First());
            else
                p.DownloadTrailers(args[0]);
        }

        public Program()
        {
            _scrapers = new List<ITrailerScraper>(new ITrailerScraper[]
                                                     {
                                                         Bootstrapper.Kernel.Get<AppleTrailerScraper>(), Bootstrapper.Kernel.Get<HdTrailersNetScraper>(),
                                                         Bootstrapper.Kernel.Get<IMDBTrailerScraper>(), Bootstrapper.Kernel.Get<DavesTrailersPageScraper>(), 
                                                         Bootstrapper.Kernel.Get<YoutubeVideoScraper>()
                                                     });
        }

        public void DownloadSingleTrailer(string directoryName)
        {
            ScrapeResult result = null;

            if (!Directory.EnumerateFiles(directoryName).Any(f => f.ToLowerInvariant().Contains("trailer")))
            {
                Console.WriteLine("\tDownload NEEDED : ");

                var movie = GetMovie(directoryName);
                if (movie == null)
                {
                    Console.WriteLine("\tMovie not found for " + directoryName);
                    return;
                }

                if (movie.Name.Length < 3)
                    return;

                foreach(var s in _scrapers)
                {
                    result = s.Scrape(movie);
                    if(result.Result == TrailerStatus.Found)
                        goto success;
                }

                return;
            }
            else
            {
                Console.WriteLine("\tnot needed...\n");
            }

            success:

            var downloadResult = DownloadTrailer(directoryName, result.Url, "wegel.ca");

            if (downloadResult != TrailerStatus.FoundAndCompletelyDownloaded)
                return; 

            var row = GetMovieRow(directoryName);
            if (row == null || row["idMovie"] == DBNull.Value || string.IsNullOrEmpty(row["idMovie"].ToString()))
                return;

            var trailerPath = _movieCollectionBasePath + (new DirectoryInfo(directoryName).Name) + "/movie-trailer.mov";
            var updateSql = string.Format("UPDATE movie set c19 = '{0}' WHERE idMovie = {1}", MySqlHelper.DoubleQuoteString(trailerPath),
                                          row["idMovie"]);

            MySqlHelper.ExecuteDataRow(_connectionString, updateSql);
        }

        public void DownloadTrailers(string basePath)
        {
            var allDirectories = Directory.EnumerateDirectories(basePath);
            var orderedDirectories = allDirectories.OrderByDescending(Directory.GetCreationTime);
            foreach (var directoryName in orderedDirectories.Where(d => !Directory.EnumerateFiles(d).Any(f => f.ToLowerInvariant().Contains("trailer")) && !Directory.EnumerateFiles(d).Any(f => f.ToLowerInvariant() == ".skip_trailer_download")))
            {
                Console.WriteLine("Processing " + directoryName + "");
                DownloadSingleTrailer(directoryName);
            }
        }

        private TrailerStatus DownloadTrailer(string directory, string downloadLink, string referrer = "")
        {
            var trailerPath = directory + "/movie-trailer.mov";
            var fi = new FileInfo(trailerPath);
            var rd = new RestartableDownload(downloadLink, trailerPath, referrer);
            var trailerLength = rd.GetContentLength();
            if (trailerLength < 0)
                return TrailerStatus.NetworkError;

            while (!fi.Exists || fi.Length != trailerLength)
            {
                rd.StartDownload();
                fi.Refresh();
            }

            return TrailerStatus.FoundAndCompletelyDownloaded;
        }

        private Movie GetMovie(string directoryPath)
        {
            DirectoryInfo info = new DirectoryInfo(directoryPath);
            string directoryName = info.Name;

            var cleanedUpName = directoryName.Replace(".", " ").ToLower();

            if(directoryName.Equals("downloading", StringComparison.InvariantCultureIgnoreCase))
                return null;

            var sizePosition = cleanedUpName.IndexOf("720p");
            if (sizePosition > 0)
            {
                cleanedUpName = cleanedUpName.Substring(0, sizePosition);
            }

            sizePosition = cleanedUpName.IndexOf("1080p");
            if (sizePosition > 0)
            {
                cleanedUpName = cleanedUpName.Substring(0, sizePosition);
            }

            sizePosition = cleanedUpName.IndexOf("bluray");
            if (sizePosition > 0)
            {
                cleanedUpName = cleanedUpName.Substring(0, sizePosition);
            }

            cleanedUpName = cleanedUpName.RemoveOccurenceOfAny(new [] {"unrated", "extended", "director's", "director", "cut", "proper", "repack", "french", "hdtv", "oar"}, false);

            cleanedUpName = cleanedUpName.Trim();

            cleanedUpName = cleanedUpName.Replace("&", "and");

            if(IsNumeric(cleanedUpName.Split(' ').Last()))
            {
                var tmp = cleanedUpName.Split(' ');

                cleanedUpName = string.Join(" ", tmp.Take(tmp.Count() - 1));
            }

            Console.WriteLine("\tCleaned movie title for search: " + cleanedUpName);

            var imdbId = GetMovieIDFromIMDB(cleanedUpName);

            Movie movie = null;
            if (string.IsNullOrEmpty(imdbId) == false)
            {
                movie = SearchMovieFromOMDBAPI(directoryPath, imdbId);
            }

            if (movie == null)
            {
                movie = SearchMovieFromOMDBAPI(directoryPath, cleanedUpName) ??
                GetMovieFromGoogleThenOMDBAPI(directoryPath, cleanedUpName);
            }
             

            if(movie != null)
                Console.WriteLine("\tFound movie name: " + movie.Name + "(" + movie.ImdbId + ")");
            else
                Console.WriteLine("\tCouldn't get movie name or id");

            return movie;
        }

        private static string GetMovieIDFromIMDB(string cleanedUpName)
        {
            var doc = new HtmlWeb().Load("http://www.imdb.com/find?q=" + cleanedUpName);

            var links = doc.DocumentNode.Descendants("td")
                .Where(td => td.Attributes.Contains("class") && td.Attributes["class"].Value == "result_text")
                .Select(td => td.Descendants("a").FirstOrDefault());

            if (links.Any() == false)
                return null;

            return links.First().Attributes["href"].Value.Replace("/title/", "").Split(new[] {'/'})[0];
         }    

        private static Movie GetMovieFromGoogleThenOMDBAPI(string directory, string cleanedUpName)
        {
            using (var web = new WebClient())
            {
                var searchTerm = "site:imdb.com " + cleanedUpName;

                var googleResult = Bootstrapper.Kernel.Get<IGoogleSearch>().Search(searchTerm, "http://wegel.ca");

                var firstRelevantResult = googleResult.responseData.results.FirstOrDefault(r => r.unescapedUrl.Contains("www.imdb.com/title/"));

                if (firstRelevantResult == null)
                    return null;

                var linkFromGoogle = firstRelevantResult.unescapedUrl;

                var imdbId = linkFromGoogle.Replace("http://www.imdb.com/title/", "").Replace("/", "");

                var result = web.DownloadString(String.Format(
                    "http://www.omdbapi.com/?i={0}",
                    imdbId));

                var o = JObject.Parse(result);


                if (((string)o.SelectToken("Response")).Equals("True", StringComparison.InvariantCultureIgnoreCase) == false)
                {
                    return null;
                }


                var movie = new Movie();
                movie.Name = (string)o.SelectToken("Title");
                movie.ImdbId = (string)o.SelectToken("imdbID");
                movie.Year = (string)o.SelectToken("Year");
                movie.Director = (string)o.SelectToken("Director");
                movie.Directory = directory;

                return movie;
            }
        }

        private static Movie SearchMovieFromOMDBAPI(string directoryPath, string cleanedUpName)
        {
            var web = new WebClient();

            web.Headers.Add("Referrer", "http://wegel.ca/");
            var result = web.DownloadString(String.Format(
                "http://www.omdbapi.com/?{0}={1}", cleanedUpName.StartsWith("tt") ? "i" : "s",
                cleanedUpName));

            JObject o = JObject.Parse(result);


            if (o == null || o.SelectToken("Response") == null || o.SelectToken("Response").ToString().Equals("True", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                return null;
            }


            var movie = new Movie();
            movie.Name = (string) o.SelectToken("Title");
            movie.ImdbId = (string)o.SelectToken("imdbID");
            movie.Year = (string) o.SelectToken("Year");
            movie.Director = (string)o.SelectToken("Director");
            movie.Directory = directoryPath;

            return movie;
        }

        public static Boolean IsNumeric(string stringToTest)
        {
            int result;
            return int.TryParse(stringToTest, out result);
        }

        private DataRow GetMovieRow(string directoryPath)
        {
            var di = new DirectoryInfo(directoryPath);
            StringBuilder  var1 = new StringBuilder();
            var1.Append("SELECT m.* \n");
            var1.Append("FROM   path p \n");
            var1.Append("       LEFT JOIN files f \n");
            var1.Append("                 LEFT JOIN movie m \n");
            var1.Append("                   ON f.idfile = m.idfile \n");
            var1.Append("         ON p.idpath = f.idpath \n");
            var1.Append(string.Format("WHERE  strpath LIKE '%/{0}/' \n", MySqlHelper.DoubleQuoteString(di.Name)));
            var1.Append("LIMIT  1 ");

            return MySqlHelper.ExecuteDataRow(_connectionString, var1.ToString());
        }
    }


    [DebuggerDisplay("Movie = {Name}")]
    public class Movie
    {
        public string Name { get; set; }
        public string ImdbId { get; set; }
        public string Directory { get; set; }
        public string Year { get; set; }
        public string Director { get; set; }

        public string NameWithoutSpacesAndStuff()
        {
            return Name.ToLowerInvariant().Replace(" ", "").Replace("'", "").Replace("-", "");
        }

        public string NameWithoutPunctuation()
        {
            return Regex.Replace(Name, @"[^\w\s]", "");
        }
    }

    public static class Extensions
    {
        public static string Replace(this string str, string oldValue, string newValue, StringComparison comparison)
        {
            StringBuilder sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        public static string RemoveOccurenceOfAny(this string input, IEnumerable<string> toRemove, bool caseSensitive = true)
        {
            return toRemove.Aggregate(input, (current, s) => current.Replace(s, "", caseSensitive? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase));
        }

        public static ExpandoObject ToExpando(this string json)
        {
 
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            IDictionary< string, object > dictionary = serializer.Deserialize< IDictionary< string, object> >(json);
            return dictionary.Expando();
 
        }

        public static ExpandoObject Expando(this IDictionary< string, object > dictionary)
        {
            ExpandoObject expandoObject = new ExpandoObject();
            IDictionary< string, object > objects = expandoObject;
 
            foreach (var item in dictionary)
            {
                bool processed = false;
 
                if (item.Value is IDictionary< string, object >)
                {
                    objects.Add(item.Key, Expando((IDictionary< string, object >)item.Value));
                    processed = true;
                }
                else if (item.Value is ICollection)
                {
                    List< object > itemList = new List< object >();
 
                    foreach (var item2 in (ICollection)item.Value)
 
                        if (item2 is IDictionary< string, object >)
                            itemList.Add(Expando((IDictionary< string, object >)item2));
                        else
                            itemList.Add(Expando(new Dictionary< string, object > { { "Unknown", item2 } }));
 
                    if (itemList.Count > 0)
                    {
                        objects.Add(item.Key, itemList);
                        processed = true;
                    }
                }
 
                if (!processed)
                    objects.Add(item);
            }
 
            return expandoObject;
        }
    }

    public static class DamerauLevenshtein
    {
        public static int DamerauLevenshteinDistanceTo(this string @string, string targetString)
        {
            return DamerauLevenshteinDistance(@string, targetString);
        }

        public static int DamerauLevenshteinDistance(string string1, string string2)
        {
            if (String.IsNullOrEmpty(string1))
            {
                if (!String.IsNullOrEmpty(string2))
                    return string2.Length;

                return 0;
            }

            if (String.IsNullOrEmpty(string2))
            {
                if (!String.IsNullOrEmpty(string1))
                    return string1.Length;

                return 0;
            }

            int length1 = string1.Length;
            int length2 = string2.Length;

            int[,] d = new int[length1 + 1, length2 + 1];

            int cost, del, ins, sub;

            for (int i = 0; i <= d.GetUpperBound(0); i++)
                d[i, 0] = i;

            for (int i = 0; i <= d.GetUpperBound(1); i++)
                d[0, i] = i;

            for (int i = 1; i <= d.GetUpperBound(0); i++)
            {
                for (int j = 1; j <= d.GetUpperBound(1); j++)
                {
                    if (string1[i - 1] == string2[j - 1])
                        cost = 0;
                    else
                        cost = 1;

                    del = d[i - 1, j] + 1;
                    ins = d[i, j - 1] + 1;
                    sub = d[i - 1, j - 1] + cost;

                    d[i, j] = Math.Min(del, Math.Min(ins, sub));

                    if (i > 1 && j > 1 && string1[i - 1] == string2[j - 2] && string1[i - 2] == string2[j - 1])
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + cost);
                }
            }

            return d[d.GetUpperBound(0), d.GetUpperBound(1)];
        }
    }
}
