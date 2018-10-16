// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Export
{
    public interface IEventExporter
    {
        /// <summary>
        /// Exports <paramref name="eventEntity"/> as a <see cref="Event"/>. If it should not be exported, returns <c>null</c>.
        /// </summary>
        Event Export(EventEntity eventEntity);
    }
}