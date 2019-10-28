// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;

namespace NgTests
{
    public class TestableTelemetry : ITelemetry, ISupportProperties
    {
        private readonly IDictionary<string, string> _properties = new Dictionary<string, string>();

        public DateTimeOffset Timestamp { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public TelemetryContext Context => throw new NotImplementedException();

        public IExtension Extension { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string Sequence { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IDictionary<string, string> Properties => _properties;

        public ITelemetry DeepClone()
        {
            throw new NotImplementedException();
        }

        public void Sanitize()
        {
            throw new NotImplementedException();
        }

        public void SerializeData(ISerializationWriter serializationWriter)
        {
            throw new NotImplementedException();
        }
    }
}
