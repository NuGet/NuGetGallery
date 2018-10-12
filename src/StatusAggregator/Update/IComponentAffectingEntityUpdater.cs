// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Update
{
    public interface IComponentAffectingEntityUpdater<T>
        where T : ComponentAffectingEntity
    {
        /// <summary>
        /// Updates <paramref name="groupEntity"/> given that the current time is now <paramref name="cursor"/>.
        /// Returns whether <paramref name="groupEntity"/> is inactive.
        /// </summary>
        Task UpdateAsync(T entity, DateTime cursor);
    }
}
