using System;
using PowerArgs;

namespace MetadataClient
{
    class Program
    {
        static void Main(string[] args)
        {
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
