// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Status;

namespace StatusAggregator.Export
{
    public interface IEventsExporter
    {
        /// <summary>
        /// Exports recent events.
        /// </summary>
        IEnumerable<Event> Export(DateTime cursor);
    }
}