// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public class SimpleBlobStorageConfiguration : IBlobStorageConfiguration
    {
        public string ConnectionString
        {
            get;
            private set;
        }

        public bool ReadAccessGeoRedundant
        {
            get;
            private set;
        }

    public SimpleBlobStorageConfiguration(string connectionString, bool readAccessGeoRedundant)
        {
            ConnectionString = connectionString;
            ReadAccessGeoRedundant = readAccessGeoRedundant;
        }
    }
}
