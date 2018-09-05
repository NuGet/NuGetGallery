// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class TimestampComparisonException : ValidationException
    {
        public PackageTimestampMetadata TimestampV2 { get; }
        public PackageTimestampMetadata TimestampCatalog { get; }

        public TimestampComparisonException(PackageTimestampMetadata timestampV2, PackageTimestampMetadata timestampCatalog, string message)
            : base(message)
        {
            TimestampV2 = timestampV2;
            TimestampCatalog = timestampCatalog;

            Data.Add(nameof(TimestampV2), JsonConvert.SerializeObject(TimestampV2));
            Data.Add(nameof(TimestampCatalog), JsonConvert.SerializeObject(TimestampCatalog));
        }
    }
}
