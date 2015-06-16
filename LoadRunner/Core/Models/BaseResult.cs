using System;

namespace Org.LoadRunner.Core.Models
{
    internal class BaseResult
    {
        public DateTime StartedTime { get; set; }
        public DateTime FinishedTime { get; set; }
        public double CompletedTime { get; set; }
        public int Bytes { get; set; }
        public string Message { get; set; }
        public bool IsSuccessful { get; set; }
    }
}
