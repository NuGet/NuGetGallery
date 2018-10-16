// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    /// <summary>
    /// Creates a <typeparamref name="TEntity"/> given a <see cref="ParsedIncident"/>.
    /// </summary>
    public interface IComponentAffectingEntityFactory<TEntity>
        where TEntity : TableEntity
    {
        Task<TEntity> CreateAsync(ParsedIncident input);
    }
}
