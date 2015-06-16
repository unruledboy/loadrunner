using System;

namespace Org.LoadRunner.Core.Models
{
    internal class BatchLoadEventArgs : EventArgs
    {
        public BatchPayload Load { get; set; }
        public string RequestorAddress { get; set; }
    }
}
