using System;
using System.IO;
using System.Net;

namespace TrailerDownloader
{
    public class RestartableDownload
    {
        private Uri _uri;
        private string _destFile;
        private FileStream _writeStream;
        private Stream _readStream;
        private long _length;
        private string _referrer;

        internal RestartableDownload(string uri, string destinationFile, string referrer = "")
        {
            _uri = new Uri(uri);
            _destFile = destinationFile;
            _referrer = referrer;
        }
   
        internal void StartDownload()
        {
            if (_uri.Scheme.Equals("http"))
            {
                try
                {
                    long start = OpenWriteStream();
                    long length = GetContentLength();
                    if (start < length)
                    {
                        OpenReadStream(start, length);
                        Copy();
                    }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    if (_writeStream != null)
                        _writeStream.Close();
                    if (_readStream != null)
                        _readStream.Close();
                }
            }
        }
   
        private long OpenWriteStream()
        {
            _writeStream = new FileStream(_destFile, FileMode.Append, FileAccess.Write);
            return _writeStream.Length;
        }
   
        public long GetContentLength()
        {
            if(_length != 0)
                return _length;
              
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_uri);
            request.UserAgent = "QuickTime";
            request.Referer = _referrer;

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                _length = response.ContentLength;
                response.Close();
            }
            catch (WebException wex)
            {
                return -1;
                throw;
            }
              
            return _length;
        }
   
        private void OpenReadStream(long start, long length)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_uri);
            request.UserAgent = "QuickTime";
            request.Referer = _referrer;

            request.AddRange((int)start, (int)length);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response.ContentLength == length)
            {
                _writeStream.Seek(0, SeekOrigin.Begin);
            }
            _readStream = response.GetResponseStream();
        }
   
        private void Copy()
        {
            byte[] buffer = new byte[1024*1024];
            int count = _readStream.Read(buffer, 0, buffer.Length);
            while (count > 0)
            {
                _writeStream.Write(buffer, 0, count);
                _writeStream.Flush();
                count = _readStream.Read(buffer, 0, buffer.Length);
            }
        }
    }
}