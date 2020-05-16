// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Common;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IDatabase
    {
        IDbContextTransaction BeginTransaction();

        Task<int> ExecuteSqlCommandAsync(string sql, params object[] parameters);

        Task<int> ExecuteSqlResourceAsync(string name, params object[] parameters);
    }
}