using Resolver.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Resolver
{
    class TreeSplitter
    {
        public static List<PNode> FindIndependentTrees(PNode original)
        {
            List<Subtree> subtrees = new List<Subtree>();

            PVNode parent = original.Children.First();

            foreach (PNode pnode in parent.Children)
            {
                //Console.Write("{0}: ", pnode.Id);

                HashSet<string> participants = new HashSet<string>();
                participants.Add(pnode.Id);

                GetParticipants(pnode, participants);

                bool newSubtreeNeeded = true;
                foreach (Subtree subtree in subtrees)
                {
                    if (subtree.HasOverlap(participants))
                    {
                        subtree.Roots.Add(pnode);
                        newSubtreeNeeded = false;
                        break;
                    }
                }

                if (newSubtreeNeeded)
                {
                    Subtree newSubtree = new Subtree();
                    newSubtree.Roots.Add(pnode);

                    foreach (string s in participants)
                    {
                        newSubtree.Participants.Add(s);
                    }

                    subtrees.Add(newSubtree);
                }

                //foreach (string participant in participants)
                //{
                //    Console.Write("{0} ", participant);
                //}
                //Console.WriteLine();
            }

            List<PNode> result = new List<PNode>();

            foreach (Subtree subtree in subtrees)
            {
                PNode newRoot = new PNode("$");
                PVNode newRootVersion = new PVNode(new SemanticVersion(0), new Package("$", SemanticVersion.Min, null));
                newRoot.Children.Add(newRootVersion);

                foreach (PNode newRootChild in subtree.Roots)
                {
                    newRootVersion.AddChild(newRootChild);
                }

                result.Add(newRoot);
            }

            return result;
        }

        class Subtree
        {
            public List<PNode> Roots = new List<PNode>();
            public HashSet<string> Participants = new HashSet<string>();

            public bool HasOverlap(HashSet<string> p)
            {
                foreach (string s in p)
                {
                    if (Participants.Contains(s))
                    {
                        return true;
                    }
                }
                return false;
            }

            public void Print()
            {
                foreach (PNode pnode in Roots)
                {
                    Console.Write("{0} ", pnode.Id);
                }
                Console.WriteLine();
            }
        }

        public static void GetParticipants(PVNode pvnode, HashSet<string> participants)
        {
            foreach (PNode child in pvnode.Children)
            {
                GetParticipants(child, participants);
            }
        }

        private static void GetParticipants(PNode pnode, HashSet<string> participants)
        {
            participants.Add(pnode.Id);

            foreach (PVNode child in pnode.Children)
            {
                GetParticipants(child, participants);
            }
        }
    }
}
