// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;


namespace StatusAggregator.Table
{
    public interface ITableWrapper
    {
        Task CreateIfNotExistsAsync();

        Task<T> Retrieve<T>(string partitionKey, string rowKey) 
            where T : class, ITableEntity;

        Task InsertOrReplaceAsync(ITableEntity tableEntity);

        IQueryable<T> CreateQuery<T>() where T : ITableEntity, new();
    }
}
