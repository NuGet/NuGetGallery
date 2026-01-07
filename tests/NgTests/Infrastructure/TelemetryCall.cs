// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NgTests.Infrastructure
{
    public class TelemetryCall
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }

        internal TelemetryCall(string name, IDictionary<string, string> properties)
        {
            Name = name;

            if (properties != null)
            {
                Properties = new ReadOnlyDictionary<string, string>(properties);
            }
        }
    }
}