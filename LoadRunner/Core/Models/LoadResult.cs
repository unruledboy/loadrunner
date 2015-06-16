using System;
using System.Collections.Generic;

namespace Org.LoadRunner.Core.Models
{
    internal class LoadResult
    {
        public DateTime StartedTime { get; set; }
        public DateTime FinishedTime { get; set; }
        public double TotalTime { get; set; }
        public int Completed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public int Requests { get; set; }
        public long BytesPerRequest { get; set; }
        public double RequestPerSecond { get; set; }
        public double AvgRequestTime { get; set; }

        public double AvgTime { get; set; }
        public double HitsPerSecond { get; set; }
        public long TotalBytes { get; set; }
        public long BytesPerHit { get; set; }
        public string Message { get; set; }
        public IList<ItemResult> Items { get; set; }
        public double MinAvgTime { get; set; }
        public double MinHitsPerSecond { get; set; }
        public double MaxAvgTime { get; set; }
        public double MaxHitsPerSecond { get; set; }
    }
}
