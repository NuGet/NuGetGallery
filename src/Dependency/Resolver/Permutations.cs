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
        static bool Loop(List<Tuple<string, SemanticVersion>>[] x, int i, Stack<Tuple<string, SemanticVersion>> candidate, Func<List<Tuple<string, SemanticVersion>>, bool> test)
        {
            if (i == x.Length)
            {
                List<Tuple<string, SemanticVersion>> list = new List<Tuple<string, SemanticVersion>>(candidate);
                return test(list);
            }

            foreach (Tuple<string, SemanticVersion> s in x[i])
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

        public static void Run(List<Tuple<string, SemanticVersion>>[] x, Func<List<Tuple<string, SemanticVersion>>, bool> test)
        {
            Stack<Tuple<string, SemanticVersion>> candidate = new Stack<Tuple<string, SemanticVersion>>();
            Loop(x, 0, candidate, test);
        }
    }
}
