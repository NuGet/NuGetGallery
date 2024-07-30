// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Exceptions
{
    public class AggregateMetadataInconsistencyException<T> : MetadataInconsistencyException
    {
        public AggregateMetadataInconsistencyException(IReadOnlyCollection<MetadataInconsistencyException<T>> innerExceptions)
            : base(null)
        {
            InnerExceptions = innerExceptions ?? throw new ArgumentNullException(nameof(innerExceptions));
            Data.Add("InnerExceptions", JsonConvert.SerializeObject(InnerExceptions));
        }

        public IReadOnlyCollection<MetadataInconsistencyException<T>> InnerExceptions { get; }
    }
}
