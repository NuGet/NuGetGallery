// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class MetadataFieldInconsistencyException<TMetadata> : MetadataInconsistencyException<TMetadata>
    {
        public MetadataFieldInconsistencyException(TMetadata v2Metadata, TMetadata v3Metadata, string fieldName, Func<TMetadata, object> getField)
            : this(v2Metadata, v3Metadata, fieldName, getField(v2Metadata), getField(v3Metadata))
        {
        }

        public MetadataFieldInconsistencyException(TMetadata v2Metadata, TMetadata v3Metadata, string fieldName, object v2Field, object v3Field)
            : base(v2Metadata, v3Metadata, $"{fieldName} does not match!")
        {
            Data.Add($"{nameof(V2Metadata)}.{fieldName}", v2Field);
            Data.Add($"{nameof(V3Metadata)}.{fieldName}", v3Field);
        }
    }
}
