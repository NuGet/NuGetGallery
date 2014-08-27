using System;
using System.Threading;

namespace Catalog.Updater
{
    class Program
    {
        static void Main(string[] args)
        {
            var job = new Job();
            if(!job.Init(args))
            {
                job.ShowHelp();
                return;
            }

            while(true)
            {
                job.Run();
                Thread.Sleep(5000);
            }
        }
    }
}
