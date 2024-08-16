// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class MetadataInconsistencyException : ValidationException
    {
        public MetadataInconsistencyException(string additionalMessage)
            : base("The metadata between the database and V3 is inconsistent!" +
                  (additionalMessage != null ? $" {additionalMessage}" : ""))
        {
        }
    }

    public class MetadataInconsistencyException<T> : MetadataInconsistencyException
    {
        public T DatabaseMetadata { get; private set; }
        public T V3Metadata { get; private set; }

        public MetadataInconsistencyException(T databaseMetadata, T v3Metadata)
            : this(databaseMetadata, v3Metadata, null)
        {
        }

        public MetadataInconsistencyException(T databaseMetadata, T v3Metadata, string additionalMessage)
            : base(additionalMessage)
        {
            DatabaseMetadata = databaseMetadata;
            V3Metadata = v3Metadata;

            Data.Add(nameof(DatabaseMetadata), JsonConvert.SerializeObject(DatabaseMetadata));
            Data.Add(nameof(V3Metadata), JsonConvert.SerializeObject(V3Metadata));
        }
    }
}
