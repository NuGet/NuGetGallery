// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Serialization;

namespace Stats.CreateAzureCdnWarehouseReports
{
    public class NuGetContractResolver
        : CamelCasePropertyNamesContractResolver
    {
        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            // Don't camel case dictionary keys
            JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
            contract.DictionaryKeyResolver = s => s;
            return contract;
        }
    }
}