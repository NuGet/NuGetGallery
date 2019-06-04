// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Defines the column names for db2catalog SQL queries.
    /// </summary>
    public static class Db2CatalogProjectionColumnNames
    {
        public const string PackageId = "Id";
        public const string NormalizedVersion = "NormalizedVersion";
        public const string Listed = "Listed";
        public const string HideLicenseReport = "HideLicenseReport";
        public const string Created = "Created";
        public const string LastEdited = "LastEdited";
        public const string Published = "Published";
        public const string LicenseNames = "LicenseNames";
        public const string LicenseReportUrl = "LicenseReportUrl";
    }
}