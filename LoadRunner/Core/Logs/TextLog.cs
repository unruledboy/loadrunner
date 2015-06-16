using System.IO;

namespace Org.LoadRunner.Core.Logs
{
    internal class TextLog : ILog
    {
        private string _filename;
        private static readonly object _syncRoot = new object();

        internal TextLog(string filename)
        {
            this._filename = filename;
        }

        public void Add(string content)
        {
            lock (_syncRoot)
            {
                File.AppendAllText(_filename, content + "\r\n");
            }
        }
    }
}
