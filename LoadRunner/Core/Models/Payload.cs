using System.Collections.Generic;

namespace Org.LoadRunner.Core.Models
{
    public class Payload
    {
        public List<HttpRequest> Requests { get; set; }
        public HttpRequest Auth { get; set; }
        public int LoadSize { get; set; }
        public int ConcurrentSize { get; set; }
        public int ThroughputSize { get; set; }
    }
}
