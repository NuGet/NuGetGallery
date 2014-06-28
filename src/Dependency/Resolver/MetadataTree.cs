using Resolver.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Resolver
{
    public static class MetadataTree
    {
        //  Metadata tree building

        public static async Task<PNode> GetTree(string[] registrationIds, IGallery gallery, string name)
        {
            PNode root = new PNode("$");
            PVNode rootVersion = new PVNode(SemanticVersion.Min);
            root.Children.Add(rootVersion);

            foreach (string registrationId in registrationIds)
            {
                Registration registration = await gallery.GetRegistration(registrationId);

                PNode pnode = new PNode(registration.Id);
                rootVersion.AddChild(pnode);

                List<Task> trees = new List<Task>(registration.Packages.Count);

                foreach (Package package in registration.Packages)
                {
                    trees.Add(InnerGetTree(package, gallery, pnode, name));
                }

                await Task.WhenAll(trees.ToArray());
            }

            return root;
        }

        static async Task InnerGetTree(Package package, IGallery gallery, PNode parent, string name)
        {
            try
            {
                PVNode pvnode = new PVNode(package.Version);
                parent.Children.Add(pvnode);

                ICollection<Dependency> dependencies = GetDependencies(package, name);

                if (dependencies != null)
                {
                    TaskCompletionSource<int> source = new TaskCompletionSource<int>();

                    foreach (Dependency dependency in dependencies)
                    {
                        Registration registration = await gallery.GetRegistration(dependency.Id);

                        PNode pnode = new PNode(dependency.Id);
                        pvnode.AddChild(pnode);

                        List<Task> trees = new List<Task>(registration.Packages.Count);

                        foreach (Package nextPackage in registration.Packages)
                        {
                            if (dependency.Range.Includes(nextPackage.Version))
                            {
                                trees.Add(InnerGetTree(nextPackage, gallery, pnode, name));
                            }
                        }

                        await Task.WhenAll(trees.ToArray());
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("package: {0}/{1}", package.Id, package.Version), e);
            }
        }

        public static ICollection<Dependency> GetDependencies(Package package, string name)
        {
            ICollection<Dependency> dependencies = null;

            //  really what is the correct logic here? (this is currently a fallback)
            Group dependencyGroup;
            dependencyGroup = package.DependencyGroups.Where(g => g.TargetFramework == name).FirstOrDefault();

            if (dependencyGroup == null)
            {
                dependencyGroup = package.DependencyGroups.Where(g => g.TargetFramework == "all").FirstOrDefault();
            }

            if (dependencyGroup != null)
            {
                dependencies = dependencyGroup.Dependencies;
            }

            return dependencies;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //  Testing a candidate solution against a tree

        public static bool Satisfy(PNode pnode, List<Tuple<string, SemanticVersion>> candidate, IDictionary<string, SemanticVersion> result)
        {
            IDictionary<string, SemanticVersion> dictionary = new Dictionary<string, SemanticVersion>();
            foreach (Tuple<string, SemanticVersion> item in candidate)
            {
                dictionary.Add(item.Item1, item.Item2);
            }
            dictionary.Add("$", new SemanticVersion(0));

            if (Satisfy(pnode, dictionary, result))
            {
                result.Remove("$");
                return true;
            }
            return false;
        }

        static bool Satisfy(PNode pnode, IDictionary<string, SemanticVersion> dictionary, IDictionary<string, SemanticVersion> result)
        {
            if (pnode.Children.Count == 0)
            {
                return true;
            }

            // for a package ANY child can satisfy

            foreach (PVNode child in pnode.Children)
            {
                if (Satisfy(child, pnode.Id, dictionary, result))
                {
                    return true;
                }
            }

            return false;
        }

        static bool Satisfy(PVNode pvnode, string id, IDictionary<string, SemanticVersion> dictionary, IDictionary<string, SemanticVersion> result)
        {
            if (dictionary.Contains(new KeyValuePair<string, SemanticVersion>(id, pvnode.Version), new KeySemantciVersionEqualityComparer()))
            {
                result[id] = pvnode.Version;

                if (pvnode.Children.Count == 0)
                {
                    return true;
                }

                // for a particular version of a package ALL children must satisfy

                foreach (PNode child in pvnode.Children)
                {
                    if (!Satisfy(child, dictionary, result))
                    {
                        return false;
                    }
                }

                return true;
            }
            return false;
        }

        class KeySemantciVersionEqualityComparer : EqualityComparer<KeyValuePair<string, SemanticVersion>>
        {
            public override bool Equals(KeyValuePair<string, SemanticVersion> x, KeyValuePair<string, SemanticVersion> y)
            {
                return (x.Key == y.Key) && (SemanticVersionRange.DefaultComparer.Compare(x.Value, y.Value) == 0);
            }
            public override int GetHashCode(KeyValuePair<string, SemanticVersion> obj)
            {
                return obj.GetHashCode(); 
            }
        }
    }
}
