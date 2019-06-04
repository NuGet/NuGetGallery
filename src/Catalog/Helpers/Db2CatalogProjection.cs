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

            var packageId = dataRecord[Db2CatalogProjectionColumnNames.PackageId].ToString();
            var normalizedPackageVersion = dataRecord[Db2CatalogProjectionColumnNames.NormalizedVersion].ToString();
            var listed = dataRecord.GetBoolean(dataRecord.GetOrdinal(Db2CatalogProjectionColumnNames.Listed));
            var hideLicenseReport = dataRecord.GetBoolean(dataRecord.GetOrdinal(Db2CatalogProjectionColumnNames.HideLicenseReport));

            var packageContentUri = _packageContentUriBuilder.Build(packageId, normalizedPackageVersion);

            return new FeedPackageDetails(
                packageContentUri,
                dataRecord.ReadDateTime(Db2CatalogProjectionColumnNames.Created).ForceUtc(),
                dataRecord.ReadNullableUtcDateTime(Db2CatalogProjectionColumnNames.LastEdited),
                listed ? dataRecord.ReadDateTime(Db2CatalogProjectionColumnNames.Published).ForceUtc() : Constants.UnpublishedDate,
                packageId,
                normalizedPackageVersion,
                hideLicenseReport ? null : dataRecord[Db2CatalogProjectionColumnNames.LicenseNames]?.ToString(),
                hideLicenseReport ? null : dataRecord[Db2CatalogProjectionColumnNames.LicenseReportUrl]?.ToString());
        }
    }
}