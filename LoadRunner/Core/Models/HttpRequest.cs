using System.Collections.Generic;
using Org.LoadRunner.Core.Infrastructure;

namespace Org.LoadRunner.Core.Models
{
    public class HttpRequest
    {
        public string Url { get; set; }
        public List<KeyValuePair<string, string>> Parameters { get; set; }
        public string ContentEncoding { get; set; }
        public bool IsPost { get; set; }
        public ContentTypes ContentType { get; set; }
        public ContentTypes AcceptType { get; set; }
        public bool IsBodyTransport { get; set; }
    }
}
