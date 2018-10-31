// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// Generic criteria used to include or exclude packages from a process.
    /// </summary>
    public interface ICriteria
    {
        /// <summary>
        /// A list of package owner usernames to exclude. This configuration takes precedence over
        /// <see cref="ByDefaultExclude"/>.
        /// </summary>
        IList<string> ExcludeOwners { get; }

        /// <summary>
        /// A list of package ID patterns used to include packages. These patterns accept wildcards. This configuration
        /// takes precedence over <see cref="ExcludeOwners"/>. For example, if a package is excluded by
        /// <see cref="ExcludeOwners"/> but has an ID matching one of the patterns, the criteria still matches that
        /// package.
        /// </summary>
        IList<string> IncludeIdPatterns { get; }
    }
}
