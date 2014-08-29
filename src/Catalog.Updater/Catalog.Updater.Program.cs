using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Catalog.Updater
{
    class Program
    {
        static void Main(string[] args)
        {
            bool runContinuously = true;
            if (args.Length > 0 && String.Equals(args[0], "dbg", StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            if (args.Length > 0 && String.Equals(args[0], "once", StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                runContinuously = false;
            }

            Console.WriteLine("Started...");
            Console.WriteLine("Running " + (runContinuously ? " continuously..." : " once..."));

            var job = new Job();
            if(!job.Init(args))
            {
                // If the job could not be initialized successfully, STOP!
                return;
            }

            do
            {
                job.Run().Wait();
                // Wait for 5 seconds and run the job again
                Thread.Sleep(5000);
            } while (runContinuously);
        }
    }
}
