// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    public abstract class BasePackageDependencyGroup<TDependency> where TDependency : PackageDependency
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("dependencies")]
        public List<TDependency> Dependencies { get; set; }

        [JsonProperty("targetFramework")]
        public string TargetFramework { get; set; }
    }
}
