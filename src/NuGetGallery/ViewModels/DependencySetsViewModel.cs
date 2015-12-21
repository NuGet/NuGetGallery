﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class DependencySetsViewModel
    {
        public DependencySetsViewModel(IEnumerable<PackageDependency> packageDependencies)
        {
            try
            {
                DependencySets = new Dictionary<string, IEnumerable<DependencyViewModel>>();

                var dependencySets = packageDependencies
                    .GroupBy(d => d.TargetFramework)
                    .OrderBy(ds => ds.Key);

                OnlyHasAllFrameworks = dependencySets.Count() == 1 && dependencySets.First().Key == null;

                foreach (var dependencySet in dependencySets)
                {
                    var targetFramework = dependencySet.Key == null
                                              ? "All Frameworks"
                                              : NuGetFramework.Parse(dependencySet.Key).ToFriendlyName();

                    if (!DependencySets.ContainsKey(targetFramework))
                    {
                        DependencySets.Add(targetFramework,
                            dependencySet.Select(d => d.Id == null ? null : new DependencyViewModel(d.Id, d.VersionSpec)));
                    }
                }
            }
            catch (Exception e)
            {
                DependencySets = null;
                QuietLog.LogHandledException(e);
                // Just set Dependency Sets to null but still render the package.
            }
        }

        public IDictionary<string, IEnumerable<DependencyViewModel>> DependencySets { get; private set; }
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
            }

            public string Id { get; private set; }
            public string VersionSpec { get; private set; }
        }
    }
}