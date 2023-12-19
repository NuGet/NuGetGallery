// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    public static class FrameworkCompatibilityService
    {
        private static readonly IFrameworkCompatibilityProvider CompatibilityProvider = DefaultCompatibilityProvider.Instance;
        private static readonly IReadOnlyList<NuGetFramework> AllSupportedFrameworks = SupportedFrameworks.AllSupportedNuGetFrameworks;
        private static readonly IReadOnlyDictionary<NuGetFramework, ISet<NuGetFramework>> CompatibilityMatrix = GetCompatibilityMatrix();

        public static ISet<NuGetFramework> GetCompatibleFrameworks(IEnumerable<NuGetFramework> packageFrameworks)
        {
            if (packageFrameworks == null)
            {
                throw new ArgumentNullException(nameof(packageFrameworks));
            }

            var allCompatibleFrameworks = new HashSet<NuGetFramework>();

            foreach (var packageFramework in packageFrameworks)
            {
                if (packageFrameworks == null || packageFramework.IsUnsupported || packageFramework.IsPCL)
                {
                    continue;
                }

                var normalizedPackageFramework = packageFramework;
                bool hasPlatformVersion = false;

                if (!string.IsNullOrEmpty(packageFramework.Platform) && (packageFramework.PlatformVersion != FrameworkConstants.EmptyVersion))
                {
                    normalizedPackageFramework = new NuGetFramework(packageFramework.Framework,
                                                                    packageFramework.Version,
                                                                    packageFramework.Platform,
                                                                    FrameworkConstants.EmptyVersion);

                    hasPlatformVersion = true;
                }

                if (CompatibilityMatrix.TryGetValue(normalizedPackageFramework, out var compatibleFrameworks))
                {
                    if (hasPlatformVersion)
                    {
                        compatibleFrameworks = compatibleFrameworks.ToHashSet(); // make a copy

                        compatibleFrameworks.Remove(normalizedPackageFramework);
                        compatibleFrameworks.Add(packageFramework);
                    }

                    allCompatibleFrameworks.UnionWith(compatibleFrameworks);
                }
                else
                {
                    allCompatibleFrameworks.Add(packageFramework);
                }
            }

            return allCompatibleFrameworks;
        }

        private static IReadOnlyDictionary<NuGetFramework, ISet<NuGetFramework>> GetCompatibilityMatrix()
        {
            var matrix = new Dictionary<NuGetFramework, ISet<NuGetFramework>>();

            foreach (var packageFramework in AllSupportedFrameworks)
            {
                var compatibleFrameworks = new HashSet<NuGetFramework>();
                matrix.Add(packageFramework, compatibleFrameworks);

                foreach (var projectFramework in AllSupportedFrameworks)
                {
                    // This compatibility check is to know if the packageFramework can be installed on a certain projectFramework
                    if (CompatibilityProvider.IsCompatible(projectFramework, packageFramework))
                    {
                        compatibleFrameworks.Add(projectFramework);
                    }
                }
            }

            return matrix;
        }
    }
}
