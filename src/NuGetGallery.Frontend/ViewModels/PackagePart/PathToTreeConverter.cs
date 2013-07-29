using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;

namespace NuGetGallery.ViewModels.PackagePart
{
    internal static class PathToTreeConverter
    {
        public static PackageItem Convert(IEnumerable<IPackageFile> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException("files");
            }

            List<IPackageFile> paths = files.ToList();
            paths.Sort((p1, p2) => String.Compare(p1.Path, p2.Path, StringComparison.OrdinalIgnoreCase));

            var root = new PackageItem("");

            List<string[]> parsedPaths = paths.Select(p => p.Path.Split('/', '\\')).ToList();
            Parse(root, parsedPaths, 0, 0, parsedPaths.Count);

            return root;
        }

        private static void Parse(PackageItem root, List<string[]> parsedPaths, int level, int start, int end)
        {
            int i = start;
            while (i < end)
            { 
                string s = parsedPaths[i][level];

                if (parsedPaths[i].Length == level + 1)
                {
                    // it's a file
                    // Starting from nuget 2.0, they use a dummy file with the name "_._" to represent
                    // an empty folder. We just ignore it. 
                    if (!s.Equals("_._", StringComparison.OrdinalIgnoreCase))
                    {
                        root.Children.Add(new PackageItem(s, root, isFile: true));
                    }
                    i++;
                }
                else
                {
                    // it's a folder
                    int j = i;
                    while (j < end &&
                           level < parsedPaths[j].Length &&
                           parsedPaths[j][level].Equals(s, StringComparison.OrdinalIgnoreCase))
                    {
                        j++;
                    }

                    var folder = new PackageItem(s, root);
                    root.Children.Add(folder);
                    Parse(folder, parsedPaths, level + 1, i, j);

                    i = j;
                }
            }
        }
    }
}