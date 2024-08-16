// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public interface IPackageStatisticsParser
    {
        PackageStatistics FromCdnLogEntry(CdnLogEntry cdnLogEntry);
    }
}