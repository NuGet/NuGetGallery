// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;

namespace StatusAggregator.Export
{
    public interface IComponentExporter
    {
        /// <summary>
        /// Exports the status of the current active entities to an <see cref="IComponent"/>.
        /// </summary>
        IComponent Export();
    }
}