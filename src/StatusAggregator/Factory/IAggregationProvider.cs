// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public interface IAggregationProvider<TChildEntity, TAggregationEntity>
        where TChildEntity : AggregatedComponentAffectingEntity<TAggregationEntity>
        where TAggregationEntity : ComponentAffectingEntity
    {
        /// <summary>
        /// Gets an aggregation that matches <paramref name="input"/>.
        /// </summary>
        Task<TAggregationEntity> GetAsync(ParsedIncident input);
    }
}
