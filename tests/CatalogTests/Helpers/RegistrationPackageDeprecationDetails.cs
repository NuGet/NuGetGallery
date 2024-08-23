// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NgTests;

namespace CatalogTests.Helpers
{
    public class RegistrationPackageDeprecation
    {
        [JsonConstructor]
        public RegistrationPackageDeprecation(
            string[] reasons,
            string message = null,
            RegistrationPackageDeprecationAlternatePackage alternatePackage = null)
        {
            Reasons = reasons;
            Message = message;
            AlternatePackage = alternatePackage;
        }

        [JsonProperty(CatalogConstants.Reasons)]
        public string[] Reasons { get; }

        [JsonProperty(CatalogConstants.Message)]
        public string Message { get; }

        [JsonProperty(CatalogConstants.AlternatePackage)]
        public RegistrationPackageDeprecationAlternatePackage AlternatePackage { get; }
    }
}
