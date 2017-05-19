// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGet.Services.Storage
{
    public abstract class StorageFactory : IStorageFactory
    {
        public abstract Storage Create(string name = null);

        public Uri BaseAddress { get; protected set; }

        public bool Verbose { get; set; }

        public override string ToString()
        {
            return BaseAddress.ToString();
        }
    }
}
