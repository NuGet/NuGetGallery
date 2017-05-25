// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class MetadataInconsistencyException : ValidationException
    {
        public MetadataInconsistencyException(string additionalMessage)
            : base("The metadata between V2 and V3 is inconsistent!" +
                  (additionalMessage != null ? $" {additionalMessage}" : ""))
        {
        }
    }

    public class MetadataInconsistencyException<T> : MetadataInconsistencyException
    {
        public T V2Metadata { get; private set; }
        public T V3Metadata { get; private set; }

        public MetadataInconsistencyException(T v2Metadata, T v3Metadata)
            : this(v2Metadata, v3Metadata, null)
        {
        }

        public MetadataInconsistencyException(T v2Metadata, T v3Metadata, string additionalMessage)
            : base(additionalMessage)
        {
            V2Metadata = v2Metadata;
            V3Metadata = v3Metadata;

            Data.Add(nameof(V2Metadata), JsonConvert.SerializeObject(V2Metadata));
            Data.Add(nameof(V3Metadata), JsonConvert.SerializeObject(V3Metadata));
        }
    }
}
