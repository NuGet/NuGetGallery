// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status;

namespace StatusAggregator.Factory
{
    public interface IComponentFactory
    {
        /// <summary>
        /// Returns the root component that describes the service.
        /// </summary>
        IComponent Create();
    }
}
