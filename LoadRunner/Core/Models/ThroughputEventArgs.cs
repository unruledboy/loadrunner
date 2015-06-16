using System;
using System.Collections.Generic;

namespace Org.LoadRunner.Core.Models
{
    internal class ThroughputEventArgs : EventArgs
    {
        public IEnumerable<ItemResult> Items;
        public double TotalTime { get; set; }
        public double AvgTime { get; set; }
        public double HitsPerSecond { get; set; }
        public long TotalBytes { get; set; }
        public long BytesPerHit { get; set; }
    }
}
