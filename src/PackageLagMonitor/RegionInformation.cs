// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class RegionInformation
    {
        public string ResourceGroup { get; set; }

        public string ServiceName { get; set; }

        public string Region { get; set; }

        /// <summary>
        /// Base url to use for queries. Used for AzureSearch case.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// One of LuceneSearch or AzureSearch depending on what type of search service this information defines.
        /// </summary>
        public ServiceType ServiceType { get; set; }
    }
}
