// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class ValidatorIdentity : IValidatorIdentity
    {
        [JsonProperty("name")]
        public string Name { get; }

        [JsonConstructor]
        public ValidatorIdentity(string name)
        {
            Name = name;
        }
    }
}
