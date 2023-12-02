// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace NuGetGallery
{
    public static class AssetFrameworkHelper
    {
        /// <summary>
        /// This method combines the logic used in restore operations to make a determination about the TFM supported by the package.
        /// We have curated a set of compatibility requirements for our needs in NuGet.org. The client logic can be found here:
        /// https://github.com/NuGet/NuGet.Client/blob/63255047fe7052cc33b763356ff995d9166f719e/src/NuGet.Core/NuGet.Commands/RestoreCommand/CompatibilityChecker.cs#L252-L294
        /// https://github.com/NuGet/NuGet.Client/blob/63255047fe7052cc33b763356ff995d9166f719e/src/NuGet.Core/NuGet.Commands/RestoreCommand/CompatibilityChecker.cs#L439-L442
        /// ...and our combination of these elements is below.
        /// The logic is essentially this:
        /// - Determine whether we're looking at a tools package. In this case we will use tools "pattern sets" (collections of file patterns
        ///   defined in <see cref="ManagedCodeConventions" />) to assess which frameworks are targeted by the package.
        /// - If this isn't a tools package, we look for build-time, runtime, content and resource file patterns
        /// For added details on the various cases, see unit tests targeting this method.
        /// </summary>
        public static IEnumerable<NuGetFramework> GetAssetFrameworks(string packageId, IReadOnlyList<PackageType> packageTypes, IList<string> packageFiles)
        {
            var supportedTFMs = Enumerable.Empty<NuGetFramework>();
            if (packageFiles != null && packageFiles.Any())
            {
                // Setup content items for analysis
                var items = new ContentItemCollection();
                items.Load(packageFiles);
                var runtimeGraph = new RuntimeGraph();
                var conventions = new ManagedCodeConventions(runtimeGraph);

                // Let's test for tools packages first--they're a special case
                var groups = new List<ContentItemGroup>();
                if (packageTypes.Count == 1 && (packageTypes[0] == PackageType.DotnetTool ||
                                                packageTypes[0] == PackageType.DotnetCliTool))
                {
                    // Only a package that is a tool package (and nothing else) will be matched against tools pattern set
                    items.PopulateItemGroups(conventions.Patterns.ToolsAssemblies, groups);
                }
                else
                {
                    // Gather together a list of pattern sets indicating the kinds of packages we wish to evaluate
                    var patterns = new[]
                    {
                        conventions.Patterns.CompileRefAssemblies,
                        conventions.Patterns.CompileLibAssemblies,
                        conventions.Patterns.RuntimeAssemblies,
                        conventions.Patterns.ContentFiles,
                        conventions.Patterns.ResourceAssemblies,
                    };

                    // Add MSBuild to this list, but we need to ensure we have package assets before they make the cut.
                    // A series of files in the right places won't matter if there's no {id}.props|targets.
                    var msbuildPatterns = new[]
                    {
                        conventions.Patterns.MSBuildFiles,
                        conventions.Patterns.MSBuildMultiTargetingFiles,
                    };

                    // We'll create a set of "groups" --these are content items which satisfy file pattern sets
                    foreach (var pattern in patterns)
                    {
                        items.PopulateItemGroups(pattern, groups);
                    }

                    // Filter out MSBuild assets that don't match the package ID and append to groups we already have
                    var msbuildGroups = new List<ContentItemGroup>();
                    foreach (var pattern in msbuildPatterns)
                    {
                        items.PopulateItemGroups(pattern, msbuildGroups);
                    }

                    foreach (var group in msbuildGroups)
                    {
                        if (HasBuildItemsForPackageId(group.Items, packageId))
                        {
                            groups.Add(group);
                        }
                    }
                }

                // Now that we have a collection of groups which have made it through the pattern set filter, let's transform them into TFMs
                supportedTFMs = groups
                    .SelectMany(p => p.Properties)
                    .Where(pair => pair.Key == ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker)
                    .Select(pair => pair.Value)
                    .Cast<NuGetFramework>()
                    .Distinct();
            }

            return supportedTFMs;
        }

        private static bool HasBuildItemsForPackageId(IEnumerable<ContentItem> items, string packageId)
        {
            foreach (var item in items)
            {
                var fileName = Path.GetFileName(item.Path);
                if (fileName == PackagingCoreConstants.EmptyFolder)
                {
                    return true;
                }

                if ($"{packageId}.props".Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if ($"{packageId}.targets".Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Framework Generation shortname identifiers used by the Search Service for framework filtering.
        /// </summary>
        public static class FrameworkGenerationIdentifiers
        {
            public const string Net = "net";

            public const string NetFramework = "netframework";

            public const string NetCoreApp = "netcoreapp";

            public const string NetStandard = "netstandard";
        }


        /// <summary>
        /// Framework Generation display names used in the Gallery UI.
        /// </summary>
        public static class FrameworkGenerationDisplayNames
        {
            public const string Net = ".NET";

            public const string NetFramework = ".NET Framework";

            public const string NetCoreApp = ".NET Core";

            public const string NetStandard = ".NET Standard";
        }
    }
}