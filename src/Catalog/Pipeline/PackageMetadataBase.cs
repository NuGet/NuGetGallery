// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public abstract class PackageMetadataBase
    {
        public abstract void Merge(PackageMetadataBase other);
        public abstract JToken ToContent(JObject frame = null);
    }
}
