using System;

namespace Org.LoadRunner.Core.Models
{
    internal class ProgressEventArgs : EventArgs
    {
        public Payload Load { get; set; }
        public LoadResult Result { get; set; }
        public bool Cancelled { get; set; }
        public int Completed { get; set; }
    }
}
