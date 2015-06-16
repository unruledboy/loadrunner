using System;

namespace Org.LoadRunner.Core.Logs
{
    internal class ConsoleLog : ILog
    {
        public void Add(string content)
        {
            Console.WriteLine(content);
        }
    }
}
