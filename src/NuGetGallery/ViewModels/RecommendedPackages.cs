// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class RecommendedPackages
    {
        /// <summary>
        /// ID of package for which recommendations were generated.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// List of IDs of recommended packages.
        /// </summary>
        public IEnumerable<string> Recommendations { get; set; }
    }
}
