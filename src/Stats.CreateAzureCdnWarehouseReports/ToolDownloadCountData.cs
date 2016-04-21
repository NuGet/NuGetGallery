// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class ToolDownloadCountData
    {

        public string ToolId { get; set; }
        public string ToolVersion { get; set; }
        public long TotalDownloadCount { get; set; }
    }
}