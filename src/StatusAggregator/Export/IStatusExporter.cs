// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;
using System;
using System.Threading.Tasks;

namespace StatusAggregator.Export
{
    public interface IStatusExporter
    {
        /// <summary>
        /// Builds a <see cref="ServiceStatus"/> and exports it to public storage so that it can be consumed by other services.
        /// </summary>
        Task Export(DateTime cursor);
    }
}