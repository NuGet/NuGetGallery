// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Table;
using System.Threading.Tasks;

namespace StatusAggregator.Manual
{
    /// <summary>
    /// Handles updating the primary storage with an instance of <see cref="ManualStatusChangeEntity"/>.
    /// </summary>
    public interface IManualStatusChangeHandler
    {
        /// <summary>
        /// Updates the primary storage with <paramref name="entity"/>, which came from <paramref name="table"/>.
        /// </summary>
        Task Handle(ITableWrapper table, ManualStatusChangeEntity entity);
    }

    /// <summary>
    /// Handles updating the primary storage with an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IManualStatusChangeHandler<T>
        where T : ManualStatusChangeEntity
    {
        /// <summary>
        /// Updates the primary storage with <paramref name="entity"/>, which is a subclass of <see cref="ManualStatusChangeEntity"/>.
        /// </summary>
        Task Handle(T entity);
    }
}
