using Resolver.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Resolver
{
    public class Runner
    {
        public static async Task<IList<Package>> ResolveDependencies(IGallery gallery, List<string> installed)
        {
            PNode pnode = await MetadataTree.GetTree(installed.ToArray(), gallery, "net40-Client");

            List<PNode> independentTrees = TreeSplitter.FindIndependentTrees(pnode);

            List<Package> solution = new List<Package>();

            foreach (PNode tree in independentTrees)
            {
                List<Package>[] lineup = Participants.Collect(tree);

                foreach (List<Package> registration in lineup)
                {
                    registration.Reverse();
                }

                IList<Package> partial = Runner.FindFirst(tree, lineup);

                if (partial == null)
                {
                    Console.Write("unable to find solution between: ");
                    Utils.PrintDistinctRegistrations(lineup);
                    Console.WriteLine();
                }
                else
                {
                    foreach (var item in partial)
                    {
                        solution.Add(item);
                    }
                }
            }
            return solution;
        }

        public static void Simulate(PNode pnode, List<Package>[] lineup, bool verbose)
        {
            int good = 0;
            int bad = 0;

            DateTime before = DateTime.Now;

            Permutations.Run(lineup, (candidate) =>
            {
                List<Package> result = new List<Package>();
                if (MetadataTree.Satisfy(pnode, candidate, result))
                {
                    if (verbose)
                    {
                        Console.Write("GOOD: ");
                        Print(candidate);
                        Console.WriteLine();
                        Console.Write("solution: ");
                        Print(result);
                    }
                    good++;
                }
                else
                {
                    if (verbose)
                    {
                        Console.Write("BAD: ");
                        Print(candidate);
                        Console.WriteLine();
                    }
                    bad++;
                }
                return false;
            });

            DateTime after = DateTime.Now;
            Console.WriteLine("duration: {0} seconds", (after - before).TotalSeconds);
            Console.WriteLine("good: {0} bad: {1}", good, bad);
        }

        public static IList<Package> FindFirst(PNode pnode, List<Package>[] lineup)
        {
            IList<Package> solution = null;
            Permutations.Run(lineup, (candidate) =>
            {
                IList<Package> result = new List<Package>();
                if (MetadataTree.Satisfy(pnode, candidate, result))
                {
                    solution = result;
                    return true;
                }
                return false;
            });
            return solution;
        }

        static void Print(List<Package> list)
        {
            foreach (Package item in list)
            {
                Console.Write("{0}/{1} ", item.Id, item.Version);
            }
        }

        static void Print(IDictionary<string, SemanticVersion> dictionary)
        {
            foreach (KeyValuePair<string, SemanticVersion> item in dictionary)
            {
                Console.Write("{0}/{1} ", item.Key, item.Value);
            }
        }
    }
}
