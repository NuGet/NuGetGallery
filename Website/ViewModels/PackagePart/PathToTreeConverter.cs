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
            paths.Sort(PackageFilePathComparer.Instance);

            var root = new PackageItem("");

            // To avoid malicious package causing stack overflow, we limit to only files that have less than 20 nested folders.
            List<string[]> parsedPaths = paths.Select(p => p.Path.Split('/', '\\'))
                                              .Where(parts => parts.Length < 20)
                                              .ToList();
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

        private class PackageFilePathComparer : IComparer<IPackageFile>
        {
            public static readonly PackageFilePathComparer Instance = new PackageFilePathComparer();

            public int Compare(IPackageFile x, IPackageFile y)
            {
                return String.Compare(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}