// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class InMemoryConfiguration : Dictionary<string, string>, IConfiguration
    {
        public string Get(string key)
        {
            string value;
            if (TryGet(key, out value))
            {
                return value;
            }

            return null;
        }

        public bool TryGet(string key, out string value)
        {
            return TryGetValue(key, out value);
        }
    }
}