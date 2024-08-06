﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Data.Entity;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ReadOnlyEntitiesContext : IReadOnlyEntitiesContext
    {
        private readonly EntitiesContext _entitiesContext; 
        
        public ReadOnlyEntitiesContext(DbConnection connection)
        { 
            _entitiesContext = new EntitiesContext(connection, readOnly: true);
        }

        public DbSet<Package> Packages
        {
            get
            {
                return _entitiesContext.Packages;
            }
            set
            {
                _entitiesContext.Packages = value;
            }
        }

        public string QueryHint => _entitiesContext.QueryHint;

        DbSet<T> IReadOnlyEntitiesContext.Set<T>()
        {
            return _entitiesContext.Set<T>();
        }

        public void SetCommandTimeout(int? seconds)
        {
            _entitiesContext.SetCommandTimeout(seconds);
        }

        public void Dispose()
        {
            _entitiesContext.Dispose();
        }

        public IDisposable WithQueryHint(string queryHint) => _entitiesContext.WithQueryHint(queryHint);
    }
}