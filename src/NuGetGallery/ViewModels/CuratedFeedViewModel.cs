// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;

namespace NuGetGallery
{
    public class CuratedFeedViewModel
    {
        public string Name { get; set; }
        public IEnumerable<string> Managers { get; set; }
        public IEnumerable<string> ExcludedPackages { get; set; }
        public IEnumerable<IncludedPackage> IncludedPackages { get; set; }

        public class IncludedPackage
        {
            public bool AutomaticallyCurated { get; set; }
            public string Id { get; set; }
        }
    }
}