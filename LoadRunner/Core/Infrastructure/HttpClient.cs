using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Org.LoadRunner.Core.Infrastructure
{
    public enum ContentTypes
    {
        None = 0,
        Form = 1,
        JSON = 2,
        XML = 3
    }

    public class HttpClient
    {
        public const string KeyContent = "Content";
        public const int StandardHTTPPort = 80;
        private const string ParameterStart = "?";
        private ConcurrentDictionary<string, CookieContainer> cookies = new ConcurrentDictionary<string, CookieContainer>();

        public string Send(Encoding encoding, string uri, string parameters, bool isPost, ContentTypes contentType, ContentTypes acceptType, string sessionKey, bool isNewSession, Dictionary<string, string> authParameters, bool isBodyTransport)
        {
            string result;
            var data = new StringBuilder(parameters);

            if (isBodyTransport)
            {
                switch (contentType)
                {
                    case ContentTypes.None:
                    case ContentTypes.Form:
                        if (authParameters.Count > 0)
                        {
                            var authData = authParameters.ToHTTPData();
                            if (data.Length > 0)
                                data.Append(Extensions.ParameterItemSplitter);
                            data.Append(authData);
                        }
                        break;
                    default:
                        break;
                }
            }

            string url;
            if (isPost)
                url = uri;
            else
                url = data.Insert(0, uri + (uri.IndexOf(ParameterStart) != -1 ? Extensions.ParameterItemSplitter.ToString() : ParameterStart)).ToString();

            var request = (HttpWebRequest)WebRequest.Create(url);
            if (isBodyTransport)
            {
                switch (contentType)
                {
                    case ContentTypes.JSON:
                    case ContentTypes.XML:
                        authParameters.ForEach(d =>
                        {
                            data.Replace(string.Format("##{0}##", d.Key), d.Value);
                        });
                        break;
                    default:
                        if (authParameters.Count > 0)
                        {
                            var authData = authParameters.ToHTTPData();
                            if (data.Length > 0)
                                data.Append("&");
                            data.Append(authData);
                        }
                        break;
                }
            }
            else
            {
                authParameters.ForEach(d =>
                {
                    request.Headers.Add(d.Key, d.Value);
                });
            }

            if (!string.IsNullOrEmpty(sessionKey))
            {
                if (isNewSession || !cookies.ContainsKey(sessionKey))
                    cookies[sessionKey] = new CookieContainer();
                request.CookieContainer = cookies[sessionKey];
            }

            switch (acceptType)
            {
                case ContentTypes.JSON:
                    request.Accept = "application/json";
                    break;
                case ContentTypes.XML:
                    request.Accept = "text/xml";
                    break;
                default:
                    break;
            }

            if (isPost)
            {
                byte[] buffer = encoding.GetBytes(data.ToString());
                request.Method = "POST";
                switch (contentType)
                {
                    case ContentTypes.Form:
                        request.ContentType = "application/x-www-form-urlencoded";
                        break;
                    case ContentTypes.JSON:
                        request.ContentType = "application/json";
                        break;
                    case ContentTypes.XML:
                        request.ContentType = "text/xml;charset=\"utf-8\"";
                        break;
                    default:
                        break;
                }
                request.ContentLength = buffer.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(buffer, 0, buffer.Length);
                    stream.Close();
                }
            }
            else
            {
                switch (contentType)
                {
                    case ContentTypes.JSON:
                        request.ContentType = "application/json";
                        break;
                    default:
                        break;
                }
            }
            var response = (HttpWebResponse)request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }

        public string Send(Encoding encoding, string url, Dictionary<string, string> parameters, bool isPost, ContentTypes contentType, ContentTypes acceptType, string sessionKey, Dictionary<string, string> authParameters, bool isBodyTransport)
        {
            var data = new StringBuilder();
            switch (contentType)
            {
                case ContentTypes.JSON:
                case ContentTypes.XML:
                    data.Append(parameters.FirstOrDefault(p => p.Key == KeyContent).Value);
                    break;
                default:
                    foreach (var item in parameters)
                    {
                        if (data.Length > 0)
                            data.Append(Extensions.ParameterItemSplitter);
                        data.Append(string.Format("{0}={1}", item.Key, Uri.EscapeDataString(item.Value)));
                    }
                    break;
            }
            return Send(encoding, url, data.ToString(), isPost, contentType, acceptType, sessionKey, false, authParameters, isBodyTransport);
        }
    }
}

