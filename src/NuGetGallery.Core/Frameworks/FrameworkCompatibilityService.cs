// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
                if ((packageFramework.Platform != string.Empty) && (packageFramework.PlatformVersion != FrameworkConstants.EmptyVersion))
                {
                    normalizedPackageFramework = new NuGetFramework(packageFramework.Framework,
                                                                    packageFramework.Version,
                                                                    packageFramework.Platform,
                                                                    FrameworkConstants.EmptyVersion);
                }

                if (CompatibilityMatrix.TryGetValue(normalizedPackageFramework, out var compatibleFrameworks))
                {
                    allCompatibleFrameworks.UnionWith(compatibleFrameworks);
                    allCompatibleFrameworks.Add(packageFramework); // If the TFM has a platform version, then only the normalized TFM gets added with the above step,
                                                                   // and we need to add the original TFM separately. 
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

            /*
            matrix.Add(SupportedFrameworks.Net60Windows7, 
                new HashSet<NuGetFramework>() {
                    SupportedFrameworks.Net60Windows, SupportedFrameworks.Net60Windows7,
                    SupportedFrameworks.Net70Windows, SupportedFrameworks.Net70Windows7 });
            */

            return matrix;
        }
    }
}
