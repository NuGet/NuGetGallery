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

        /// <summary>
        /// Sets the <see cref="QueryHint"/> for queries to the provided value until the returned disposable is disposed.
        /// Note that this method MUST NOT be called with user input.
        /// </summary>
        /// <param name="queryHint">The query hint.</param>
        /// <example>Provide "RECOMPILE" to disable query plan caching.</example>
        /// <returns>A disposable that determines the lifetime of the provided query hint.</returns>
        IDisposable WithQueryHint(string queryHint);

        /// <summary>
        /// The current query hint to use for queries. Can be set using <see cref="WithQueryHint(string)"/>.
        /// </summary>
        string QueryHint { get; }
    }
}