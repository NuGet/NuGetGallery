// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http.Formatting;
using Newtonsoft.Json;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class Serialization
    {
        public static IEnumerable<MediaTypeFormatter> MediaTypeFormatters => new[]
        {
            new JsonMediaTypeFormatter
            {
                UseDataContractJsonSerializer = false,
                SerializerSettings = new JsonSerializerSettings()
            }
        };
    }
}