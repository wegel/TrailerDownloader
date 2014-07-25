﻿using System.Collections.Generic;

namespace TrailerDownloader.Searchers
{
    public interface IGoogleSearch
    {
        GoogleResult Search(string searchTerms, string referer);
    }


    public class Result
    {
        public string GsearchResultClass { get; set; }
        public string unescapedUrl { get; set; }
        public string url { get; set; }
        public string visibleUrl { get; set; }
        public string cacheUrl { get; set; }
        public string title { get; set; }
        public string titleNoFormatting { get; set; }
        public string content { get; set; }
    }

    public class Page
    {
        public string start { get; set; }
        public int label { get; set; }
    }

    public class Cursor
    {
        public string resultCount { get; set; }
        public List<Page> pages { get; set; }
        public string estimatedResultCount { get; set; }
        public int currentPageIndex { get; set; }
        public string moreResultsUrl { get; set; }
        public string searchResultTime { get; set; }
    }

    public class ResponseData
    {
        public List<Result> results { get; set; }
        public Cursor cursor { get; set; }
    }

    public class GoogleResult
    {
        public ResponseData responseData { get; set; }
        public object responseDetails { get; set; }
        public int responseStatus { get; set; }
    }
}
