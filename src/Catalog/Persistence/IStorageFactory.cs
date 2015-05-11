// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface IStorageFactory
    {
        Storage Create(string name = null);
        Uri BaseAddress { get; }
    }
}
