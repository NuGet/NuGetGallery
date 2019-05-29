// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Utility class to project <see cref="IDataRecord"/>s retrieved by db2catalog
    /// into a format that is consumable by the catalog writer.
    /// </summary>
    public class Db2CatalogProjection
    {
        private readonly PackageContentUriBuilder _packageContentUriBuilder;

        public Db2CatalogProjection(PackageContentUriBuilder packageContentUriBuilder)
        {
            _packageContentUriBuilder = packageContentUriBuilder ?? throw new ArgumentNullException(nameof(packageContentUriBuilder));
        }

        public FeedPackageDetails FromDataRecord(IDataRecord dataRecord)
        {
            if (dataRecord == null)
            {
                throw new ArgumentNullException(nameof(dataRecord));
            }

            var packageId = dataRecord["Id"].ToString();
            var normalizedPackageVersion = dataRecord["NormalizedVersion"].ToString();
            var listed = dataRecord.GetBoolean(dataRecord.GetOrdinal("Listed"));
            var hideLicenseReport = dataRecord.GetBoolean(dataRecord.GetOrdinal("HideLicenseReport"));

            var packageContentUri = _packageContentUriBuilder.Build(packageId, normalizedPackageVersion);

            return new FeedPackageDetails(
                packageContentUri,
                dataRecord.ReadDateTime("Created").ForceUtc(),
                dataRecord.ReadNullableUtcDateTime("LastEdited"),
                listed ? dataRecord.ReadDateTime("Published").ForceUtc() : Constants.UnpublishedDate,
                packageId,
                normalizedPackageVersion,
                hideLicenseReport ? null : dataRecord["LicenseNames"]?.ToString(),
                hideLicenseReport ? null : dataRecord["LicenseReportUrl"]?.ToString());
        }
    }
}