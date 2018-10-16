// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Status;

namespace StatusAggregator.Export
{
    public interface IStatusSerializer
    {
        /// <summary>
        /// Serializes <paramref name="rootComponent"/> and <paramref name="recentEvents"/> and saves to storage with a time of <paramref name="cursor"/>.
        /// </summary>
        Task Serialize(DateTime cursor, IComponent rootComponent, IEnumerable<Event> recentEvents);
    }
}