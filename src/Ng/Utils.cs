using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng
{
    static class Utils
    {
        public static IDictionary<string, string> GetArguments(string[] args, int start)
        {
            IDictionary<string, string> result = new Dictionary<string, string>();

            if (args.Length == start)
            {
                return result;
            }

            if ((args.Length - 1) % 2 != 0)
            {
                throw new ArgumentException("unexpected number of arguments");
            }

            for (int i = 1; i < args.Length; i += 2)
            {
                result.Add(args[i], args[i + 1]);
            }

            return result;
        }
    }
}
