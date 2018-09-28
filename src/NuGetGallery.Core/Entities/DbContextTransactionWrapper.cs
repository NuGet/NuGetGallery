// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;

namespace NuGetGallery
{
    public class DbContextTransactionWrapper : IDbContextTransaction
    {
        private readonly DbContextTransaction _transaction;

        public DbContextTransactionWrapper(DbContextTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        public void Commit()
        {
            _transaction.Commit();
        }

        public void Dispose()
        {
            _transaction.Dispose();
        }
    }
}
