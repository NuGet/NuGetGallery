// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IReadOnlyEntitiesContext : IDisposable
    {
        DbSet<Package> Packages { get; set; }

        DbSet<T> Set<T>() where T : class;

        void SetCommandTimeout(int? seconds);
    }
}