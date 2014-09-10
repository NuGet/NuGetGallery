using System;
using System.Linq;
using System.Diagnostics;
using PowerArgs;

namespace MetadataClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            if (args.Length > 0 && String.Equals("dbg", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                Debugger.Launch();
            }

            try
            {
                if (args.Length == 0)
                {
                    WriteUsage();
                }
                else
                {
                    Args.InvokeAction<Arguments>(args);
                }
            }
            catch (MissingArgException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (InvalidArgDefinitionException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void WriteUsage()
        {
            ArgUsage.GetStyledUsage<Arguments>().Write();
        }
    }
}
