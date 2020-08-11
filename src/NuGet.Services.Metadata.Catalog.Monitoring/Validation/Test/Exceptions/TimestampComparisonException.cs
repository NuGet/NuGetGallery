// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class TimestampComparisonException : ValidationException
    {
        public PackageTimestampMetadata TimestampDatabase { get; }
        public PackageTimestampMetadata TimestampCatalog { get; }

        public TimestampComparisonException(PackageTimestampMetadata timestampDatabase, PackageTimestampMetadata timestampCatalog, string message)
            : base(message)
        {
            TimestampDatabase = timestampDatabase;
            TimestampCatalog = timestampCatalog;

            Data.Add(nameof(TimestampDatabase), JsonConvert.SerializeObject(TimestampDatabase));
            Data.Add(nameof(TimestampCatalog), JsonConvert.SerializeObject(TimestampCatalog));
        }
    }
}
