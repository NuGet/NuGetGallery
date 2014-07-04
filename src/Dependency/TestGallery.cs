using Resolver.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver
{
    class TestGallery
    {
        public static Gallery Create1()
        {
            Gallery gallery = new Gallery();

            gallery.AddPackage(new Package("A", "1.0.0"));
            gallery.AddPackage(new Package("A", "1.5.0"));
            gallery.AddPackage(new Package("A", "2.0.0"));
            gallery.AddPackage(new Package("A", "2.5.0"));
            gallery.AddPackage(new Package("A", "3.0.0"));
            gallery.AddPackage(new Package("A", "3.5.0"));
            gallery.AddPackage(new Package("A", "4.0.0"));

            gallery.AddPackage(new Package("B", "1.0.0"));
            gallery.AddPackage(new Package("B", "1.5.0"));
            gallery.AddPackage(new Package("B", "2.0.0"));
            gallery.AddPackage(new Package("B", "2.5.0"));
            gallery.AddPackage(new Package("B", "3.0.0"));
            
            gallery.AddPackage(new Package("B", "3.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "4.0.0") } }
                }
            });

            gallery.AddPackage(new Package("B", "4.0.0"));

            gallery.AddPackage(new Package("C", "1.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "[1.0.0,2.0.0)") } }
                }
            });

            gallery.AddPackage(new Package("C", "2.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "[2.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("C", "2.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "[2.0.0,4.0.0)") } }
                }
            });

            return gallery;
        }

        public static Gallery Create4()
        {
            Gallery gallery = new Gallery();

            gallery.AddPackage(new Package("A", "1.0.0"));
            gallery.AddPackage(new Package("A", "1.5.0"));
            gallery.AddPackage(new Package("A", "2.0.0"));
            gallery.AddPackage(new Package("A", "2.5.0"));
            gallery.AddPackage(new Package("A", "3.0.0"));
            gallery.AddPackage(new Package("A", "3.5.0"));
            gallery.AddPackage(new Package("A", "3.6.0"));
            gallery.AddPackage(new Package("A", "3.7.0"));
            gallery.AddPackage(new Package("A", "3.8.0"));
            gallery.AddPackage(new Package("A", "4.0.0"));
            gallery.AddPackage(new Package("A", "4.5.0"));
            gallery.AddPackage(new Package("A", "5.0.0"));

            gallery.AddPackage(new Package("B", "1.0.0"));
            gallery.AddPackage(new Package("B", "1.5.0"));
            gallery.AddPackage(new Package("B", "2.0.0"));
            gallery.AddPackage(new Package("B", "2.5.0"));
            gallery.AddPackage(new Package("B", "3.0.0"));
            gallery.AddPackage(new Package("B", "3.5.0"));
            gallery.AddPackage(new Package("B", "4.0.0"));

            gallery.AddPackage(new Package("C", "1.0.0"));
            gallery.AddPackage(new Package("C", "1.5.0"));
            gallery.AddPackage(new Package("C", "2.0.0"));
            gallery.AddPackage(new Package("C", "2.5.0"));
            gallery.AddPackage(new Package("C", "3.0.0"));
            gallery.AddPackage(new Package("C", "3.5.0"));
            gallery.AddPackage(new Package("C", "4.0.0"));

            //  -------- D --------

            gallery.AddPackage(new Package("D", "1.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("D", "1.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("D", "1.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("D", "2.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("D", "2.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("D", "3.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("D", "3.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("D", "4.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            //  -------- E --------

            gallery.AddPackage(new Package("E", "1.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "(2.0.0,4.0.0]"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("E", "1.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "(2.0.0,4.0.0]"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("E", "2.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "(2.0.0,4.0.0]"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("E", "2.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "(2.0.0,4.0.0]"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("E", "3.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "(2.0.0,4.0.0]"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("E", "3.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "(2.0.0,4.0.0]"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("E", "4.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "(2.0.0,4.0.0]"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            //gallery.AddPackage(new Package("E", "4.5.0")
            //{
            //    DependencyGroups =
            //    {
            //        new Group("all") { Dependencies = { new Dependency("A", "[2.0.0]"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } } }
            //    }
            //});

            gallery.AddPackage(new Package("F", "1.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("F", "1.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("F", "2.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("F", "2.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("F", "3.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("F", "3.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("F", "4.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("F", "4.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("F", "5.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("A", "2.0.0"), new Dependency("B", "2.0.0"), new Dependency("C", "2.0.0") } }
                }
            });

            gallery.AddPackage(new Package("G", "1.0.0"));
            gallery.AddPackage(new Package("G", "1.5.0"));
            gallery.AddPackage(new Package("G", "2.0.0"));
            gallery.AddPackage(new Package("G", "2.5.0"));
            gallery.AddPackage(new Package("G", "3.0.0"));
            gallery.AddPackage(new Package("G", "3.5.0"));
            gallery.AddPackage(new Package("G", "4.0.0"));
            gallery.AddPackage(new Package("G", "4.5.0"));
            gallery.AddPackage(new Package("G", "5.0.0"));
            gallery.AddPackage(new Package("G", "5.5.0"));
            gallery.AddPackage(new Package("G", "6.0.0"));
            gallery.AddPackage(new Package("G", "6.5.0"));

            gallery.AddPackage(new Package("H", "1.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "1.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "2.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "2.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "3.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "3.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "4.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "4.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "5.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "5.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "6.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("H", "6.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("G", "1.0.0") } }
                }
            });

            gallery.AddPackage(new Package("I", "1.0.0"));
            gallery.AddPackage(new Package("I", "1.5.0"));
            gallery.AddPackage(new Package("I", "2.0.0"));
            gallery.AddPackage(new Package("I", "2.5.0"));
            gallery.AddPackage(new Package("I", "3.0.0"));
            gallery.AddPackage(new Package("I", "3.5.0"));
            gallery.AddPackage(new Package("I", "4.0.0"));
            gallery.AddPackage(new Package("I", "4.5.0"));
            gallery.AddPackage(new Package("I", "5.0.0"));
            gallery.AddPackage(new Package("I", "5.5.0"));
            gallery.AddPackage(new Package("I", "6.0.0"));
            gallery.AddPackage(new Package("I", "6.5.0"));

            gallery.AddPackage(new Package("J", "1.0.0"));
            gallery.AddPackage(new Package("J", "1.5.0"));
            gallery.AddPackage(new Package("J", "2.0.0"));
            gallery.AddPackage(new Package("J", "2.5.0"));
            gallery.AddPackage(new Package("J", "3.0.0"));
            gallery.AddPackage(new Package("J", "3.5.0"));
            gallery.AddPackage(new Package("J", "4.0.0"));
            gallery.AddPackage(new Package("J", "4.5.0"));
            gallery.AddPackage(new Package("J", "5.0.0"));
            gallery.AddPackage(new Package("J", "5.5.0"));
            gallery.AddPackage(new Package("J", "6.0.0"));
            gallery.AddPackage(new Package("J", "6.5.0"));

            gallery.AddPackage(new Package("K", "1.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "1.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "2.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "2.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "3.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "3.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "4.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "4.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "5.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "5.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "6.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "6.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "7.0.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            gallery.AddPackage(new Package("K", "7.5.0")
            {
                DependencyGroups =
                {
                    new Group("all") { Dependencies = { new Dependency("I", "[1.0.0,5.0.0]"), new Dependency("J", "[3.0.0,6.0.0]") } }
                }
            });

            return gallery;
        }
    }
}
