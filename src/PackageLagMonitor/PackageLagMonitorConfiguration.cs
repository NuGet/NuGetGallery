// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Jobs.Montoring.PackageLag
{
    public class PackageLagMonitorConfiguration
    {
        public int InstancePortMinimum { get; set; }

        public string ServiceIndexUrl { get; set; }

        public string Subscription { get; set; }

        public List<RegionInformation> RegionInformations { get; set; }
    }

    public class RegionInformation
    {
        public string ResourceGroup { get; set; }

        public string ServiceName { get; set; }

        public string Region { get; set; }
    }
}
