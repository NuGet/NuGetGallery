// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGet.Frameworks;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class DependencySetsViewModel
    {
        public DependencySetsViewModel(IEnumerable<PackageDependency> packageDependencies)
        {
            try
            {
                // Create a list to hold TFM string, parsed framework, and dependencies for proper sorting
                var frameworkGroups = new List<(string tfmString, NuGetFramework framework, string friendlyName, IEnumerable<DependencyViewModel> dependencies)>();

                var dependencySets = packageDependencies.GroupBy(d => d.TargetFramework);

                OnlyHasAllFrameworks = dependencySets.Count() == 1 && dependencySets.First().Key == null;

                foreach (var dependencySet in dependencySets)
                {
                    string tfmString = dependencySet.Key;
                    string friendlyName;
                    NuGetFramework framework;

                    if (tfmString == null)
                    {
                        friendlyName = "All Frameworks";
                        framework = null;
                    }
                    else
                    {
                        framework = NuGetFramework.Parse(tfmString);
                        friendlyName = framework.ToFriendlyName();
                    }

                    var dependencies = dependencySet.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(d => d.Id == null ? null : new DependencyViewModel(d.Id, d.VersionSpec));
                    frameworkGroups.Add((tfmString, framework, friendlyName, dependencies));
                }

                // Sort by framework using NuGetFrameworkSorter, with null frameworks (All Frameworks) first
                var sortedGroups = frameworkGroups.OrderBy(g => g.framework, NullableFrameworkComparer.Instance);

                // Build an ordered list to preserve the sorting
                var orderedDependencySets = new List<KeyValuePair<string, IEnumerable<DependencyViewModel>>>();
                foreach (var group in sortedGroups)
                {
                    orderedDependencySets.Add(new KeyValuePair<string, IEnumerable<DependencyViewModel>>(group.friendlyName, group.dependencies));
                }

                DependencySets = orderedDependencySets;
            }
            catch (Exception e)
            {
                // Just set Dependency Sets to null but still render the package.
                DependencySets = null;
                QuietLog.LogHandledException(e);
            }
        }

        public IReadOnlyList<KeyValuePair<string, IEnumerable<DependencyViewModel>>> DependencySets { get; private set; }
        public bool OnlyHasAllFrameworks { get; private set; }

        public class DependencyViewModel
        {
            public DependencyViewModel(string id, string versionSpec)
            {
                Id = id;

                if (!String.IsNullOrEmpty(versionSpec))
                {
                    VersionSpec = VersionRange.Parse(versionSpec).PrettyPrint();
                }

                if (HttpContext.Current != null)
                {
                    PackageUrl = UrlHelperExtensions.Package(new UrlHelper(HttpContext.Current.Request.RequestContext), id);
                }
            }

            public string Id { get; private set; }
            public string VersionSpec { get; private set; }
            public string PackageUrl { get; private set; }
        }

        private class NullableFrameworkComparer : IComparer<NuGetFramework>
        {
            public static readonly NullableFrameworkComparer Instance = new NullableFrameworkComparer();

            public int Compare(NuGetFramework x, NuGetFramework y)
            {
                // Put "All Frameworks" (null) first
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                // Use NuGetFrameworkSorter for actual frameworks
                return NuGetFrameworkSorter.Instance.Compare(x, y);
            }
        }
    }
}
