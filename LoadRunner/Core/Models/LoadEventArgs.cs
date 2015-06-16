using System;

namespace Org.LoadRunner.Core.Models
{
    internal class LoadEventArgs : EventArgs
    {
        public Payload Load { get; set; }
        public string Message { get; set; }
    }
}
