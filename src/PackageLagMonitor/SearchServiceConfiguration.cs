// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class SearchServiceConfiguration
    {
        public int InstancePortMinimum { get; set; }

        public string Subscription { get; set; }

        public List<RegionInformation> RegionInformations { get; set; }
    }
}
