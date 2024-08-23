// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public interface IAggregationStrategy<TAggregationEntity>
        where TAggregationEntity : ComponentAffectingEntity
    {
        /// <summary>
        /// Returns whether or not an entity built from <paramref name="input"/> using a <see cref="IComponentAffectingEntityFactory{TEntity}"/> can be aggregated by <paramref name="aggregationEntity"/>.
        /// </summary>
        Task<bool> CanBeAggregatedByAsync(ParsedIncident input, TAggregationEntity aggregationEntity);
    }
}
