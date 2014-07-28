using Resolver.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Resolver.Resolver
{
    static class Utils
    {
        public static string Indent(int indent)
        {
            string s = string.Empty;
            while (indent-- > 0)
            {
                s += " ";
            }
            return s;
        }

        public static int CountIterations(List<Tuple<string, SemanticVersion>>[] lineup)
        {
            int iterations = 1;

            foreach (List<Tuple<string, SemanticVersion>> registration in lineup)
            {
                iterations *= registration.Count;
            }

            return iterations;
        }

        public static void PrintPackages(IList<Package> packages)
        {
            foreach (Package package in packages)
            {
                Console.WriteLine("{0}/{1} ", package.Id, package.Version);
            }
        }

        public static void PrintDistinctRegistrations(List<Package>[] lineup)
        {
            foreach (List<Package> registration in lineup)
            {
                Console.Write("{0} ", registration.First().Id);
            }
        }
    }
}
