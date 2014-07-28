using Resolver.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Resolver
{
    class Permutations
    {
        static bool Loop(List<Package>[] x, int i, Stack<Package> candidate, Func<List<Package>, bool> test)
        {
            if (i == x.Length)
            {
                List<Package> list = new List<Package>(candidate);
                return test(list);
            }

            foreach (Package s in x[i])
            {
                candidate.Push(s);
                if (Loop(x, i + 1, candidate, test))
                {
                    return true;
                }
                candidate.Pop();
            }

            return false;
        }

        public static void Run(List<Package>[] x, Func<List<Package>, bool> test)
        {
            Stack<Package> candidate = new Stack<Package>();
            Loop(x, 0, candidate, test);
        }
    }
}
