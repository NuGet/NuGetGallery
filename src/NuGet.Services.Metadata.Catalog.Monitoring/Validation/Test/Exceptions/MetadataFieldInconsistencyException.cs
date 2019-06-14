// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class MetadataFieldInconsistencyException<TMetadata> : MetadataInconsistencyException<TMetadata>
    {
        public MetadataFieldInconsistencyException(TMetadata databaseMetadata, TMetadata v3Metadata, string fieldName, Func<TMetadata, object> getField)
            : this(databaseMetadata, v3Metadata, fieldName, getField(databaseMetadata), getField(v3Metadata))
        {
        }

        public MetadataFieldInconsistencyException(TMetadata databaseMetadata, TMetadata v3Metadata, string fieldName, object databaseField, object v3Field)
            : base(databaseMetadata, v3Metadata, $"{fieldName} does not match!")
        {
            Data.Add($"{nameof(DatabaseMetadata)}.{fieldName}", JsonConvert.SerializeObject(databaseField, JsonSerializerUtility.SerializerSettings));
            Data.Add($"{nameof(V3Metadata)}.{fieldName}", JsonConvert.SerializeObject(v3Field, JsonSerializerUtility.SerializerSettings));
        }
    }
}
