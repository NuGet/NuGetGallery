using Resolver.Metadata;
using Resolver.Resolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver
{
    static class TestResolver
    {
        public static async Task Test0()
        {
            Gallery gallery = TestGallery.Create4();
            PNode pnode = await MetadataTree.GetTree(new string[] { "D", "E", "F", "G", "H", "K" }, gallery, "all");

            List<PNode> independentTrees = TreeSplitter.FindIndependentTrees(pnode);

            IList<Package> solution = new List<Package>();

            foreach (PNode tree in independentTrees)
            {
                List<Package>[] lineup = Participants.Collect(tree);

                // apply policy, where policy is strictly filtering and sorting on the lineup structiure

                // lineup registrations are ordered low to high version - reverse the order to find a solution using the latest
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
                    foreach (Package item in partial)
                    {
                        solution.Add(item);
                    }
                }
            }

            Utils.PrintPackages(solution);
        }

        public static async Task Test1()
        {
            //IGallery gallery = new RemoteGallery("http://nuget3.blob.core.windows.net/pub/");
            IGallery gallery = new RemoteGallery("http://localhost:8000/pub/");

            DateTime before = DateTime.Now;

            PNode pnode = await MetadataTree.GetTree(new string[] 
            { 
                "dotNetRdf",
                "WindowsAzure.Storage",
                "json-ld.net"
            },
            gallery, ".NETFramework4.0");

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds", (after - before).TotalSeconds);

            List<PNode> independentTrees = TreeSplitter.FindIndependentTrees(pnode);

            IList<Package> solution = new List<Package>();

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
                    foreach (Package item in partial)
                    {
                        solution.Add(item);
                    }
                }
            }

            Console.WriteLine("solution:");
            Console.WriteLine();
            Utils.PrintPackages(solution);
            Console.WriteLine();
        }

        public static async Task Test2()
        {
            //IGallery gallery = new RemoteGallery("http://nuget3.blob.core.windows.net/pub/");
            IGallery gallery = new RemoteGallery("http://localhost:8000/pub/");

            DateTime before = DateTime.Now;

            PNode pnode = await MetadataTree.GetTree(new string[] 
            { 
                "entityframework"
            },
            gallery, ".NETFramework4.0");

            DateTime after = DateTime.Now;

            Console.WriteLine("{0} seconds", (after - before).TotalSeconds);

            List<PNode> independentTrees = TreeSplitter.FindIndependentTrees(pnode);

            IList<Package> solution = new List<Package>();

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
                    foreach (Package item in partial)
                    {
                        solution.Add(item);
                    }
                }
            }

            Console.WriteLine("solution:");
            Console.WriteLine();
            Utils.PrintPackages(solution);
            Console.WriteLine();
        }
    }
}
