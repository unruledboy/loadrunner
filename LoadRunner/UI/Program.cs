using System;

namespace Org.LoadRunner.UI
{
    class Program
    {
        static void Main(string[] args)
        {
            var runner = new TaskRunner();
            runner.Start();
            runner.Stop();
            Console.WriteLine("all tasks done");
            Console.Read();
        }

    }
}
