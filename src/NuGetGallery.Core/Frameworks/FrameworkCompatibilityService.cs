﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    public class FrameworkCompatibilityService : IFrameworkCompatibilityService
    {
        private readonly IFrameworkCompatibilityProvider CompatibilityProvider = DefaultCompatibilityProvider.Instance;
        private readonly ISet<NuGetFramework> DotnetSupportedFrameworks = new HashSet<NuGetFramework>(DotNetSupportedFrameworks.GetSupportedFrameworks());

        private readonly IReadOnlyDictionary<NuGetFramework, ISet<NuGetFramework>> _compatibilityMatrix;

        public FrameworkCompatibilityService()
        {
            _compatibilityMatrix = GetCompatibilityMatrix();
        }

        public ISet<NuGetFramework> GetCompatibleFrameworks(IEnumerable<NuGetFramework> packageFrameworks)
        {
            if (packageFrameworks == null)
            {
                return null;
            }

            var allCompatibleFrameworks = new HashSet<NuGetFramework>();

            foreach (var packageFramework in packageFrameworks)
            {
                if (packageFrameworks == null || packageFramework.IsUnsupported || packageFramework.IsPCL)
                {
                    continue;
                }

                var compatibleFrameworks = GetCompatibleFrameworks(packageFramework);

                allCompatibleFrameworks.UnionWith(compatibleFrameworks);
            }

            return allCompatibleFrameworks;
        }

        private ISet<NuGetFramework> GetCompatibleFrameworks(NuGetFramework projectFramework)
        {
            _compatibilityMatrix.TryGetValue(projectFramework, out var compatibleFrameworks);

            if (compatibleFrameworks == null)
            {
                return GetComputedCompatibleFrameworks(projectFramework);
            }

            return compatibleFrameworks;
        }

        private ISet<NuGetFramework> GetComputedCompatibleFrameworks(NuGetFramework packageFramework)
        {
            var compatibleFrameworks = new HashSet<NuGetFramework>();

            foreach (var projectFramework in DotnetSupportedFrameworks)
            {
                if (CompatibilityProvider.IsCompatible(projectFramework, packageFramework))
                {
                    compatibleFrameworks.Add(projectFramework);
                }
            }

            return compatibleFrameworks;
        }

        private IReadOnlyDictionary<NuGetFramework, ISet<NuGetFramework>> GetCompatibilityMatrix()
        {
            var matrix = new Dictionary<NuGetFramework, ISet<NuGetFramework>>();

            foreach (var packageFramework in DotnetSupportedFrameworks)
            {
                var compatibleFrameworks = new HashSet<NuGetFramework>();
                matrix.Add(packageFramework, compatibleFrameworks);

                foreach (var projectFramework in DotnetSupportedFrameworks)
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
