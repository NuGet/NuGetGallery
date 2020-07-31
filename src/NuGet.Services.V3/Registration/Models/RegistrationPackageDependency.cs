// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Catalog;

namespace NuGet.Protocol.Registration
{
    public class RegistrationPackageDependency : PackageDependency
    {
        [JsonProperty("registration", Order = 1)]
        public string Registration { get; set; }
    }
}
