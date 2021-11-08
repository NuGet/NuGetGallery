// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGetGallery.Frameworks
{
    public interface IFrameworkCompatibilityService
    {
        /// <summary>
        /// Computes a set of compatible target frameworks from a list of target frameworks.
        /// </summary>
        /// <param name="frameworks">List of frameworks.</param>
        /// <returns>A set of computed compatible target frameworks.</returns>
        /// <remarks>
        /// Every element on the returned set is compatible with at least one of the target frameworks from the input.
        /// </remarks>
        ISet<NuGetFramework> GetCompatibleFrameworks(IEnumerable<NuGetFramework> frameworks);
    }
}