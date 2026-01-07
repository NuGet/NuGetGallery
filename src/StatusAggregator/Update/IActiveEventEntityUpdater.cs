// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Update
{
    public interface IActiveEventEntityUpdater
    {
        /// <summary>
        /// Updates all active <see cref="EventEntity"/>s.
        /// </summary>
        /// <param name="cursor">The current time.</param>
        Task UpdateAllAsync(DateTime cursor);
    }
}
