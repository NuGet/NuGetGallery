// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class ResourceSaveOperation
    {
        public Uri ResourceUri { get; set; }
        public Task SaveTask { get; set; }
    }
}