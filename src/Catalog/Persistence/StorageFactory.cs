// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class StorageFactory : IStorageFactory
    {
        public StorageFactory() : this(throttle: null)
        {
        }

        public StorageFactory(IThrottle throttle)
        {
            Throttle = throttle ?? NullThrottle.Instance;
        }

        public abstract Storage Create(string name = null);

        public Uri BaseAddress { get; protected set; }

        // For telemetry only
        public Uri DestinationAddress { get; protected set; }

        public bool Verbose { get; protected set; }

        public IThrottle Throttle { get; }

        public override string ToString()
        {
            return BaseAddress.ToString();
        }
    }
}