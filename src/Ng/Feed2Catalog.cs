using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng
{
    public static class Feed2Catalog
    {
        public static void Run(string[] args)
        {
            IDictionary<string, string> arguments = Utils.GetArguments(args, 1);

            foreach (var arg in arguments)
            {
                Console.WriteLine("{0} = {1}", arg.Key, arg.Value);
            }
        }
    }
}
