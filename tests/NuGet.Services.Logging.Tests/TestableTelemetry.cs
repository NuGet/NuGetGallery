// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.Logging.Tests
{
    public class TestableTelemetry : ITelemetry, ISupportProperties
    {
        public DateTimeOffset Timestamp { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public TelemetryContext Context => throw new NotImplementedException();

        public IExtension Extension { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string Sequence { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

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
