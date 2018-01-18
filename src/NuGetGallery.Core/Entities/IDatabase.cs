// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IDatabase
    {
        DbContextTransaction BeginTransaction();

        Task<int> ExecuteSqlCommandAsync(string sql, params object[] parameters);

        Task<int> ExecuteSqlResourceAsync(string name, params object[] parameters);
    }
}
