// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public interface IAffectedComponentPathProvider<T> 
        where T : IComponentAffectingEntity
    {
        /// <summary>
        /// Returns the <see cref="IComponentAffectingEntity.AffectedComponentPath"/> to use to create an instance of <typeparamref name="T"/> for <paramref name="input"/>.
        /// </summary>
        string Get(ParsedIncident input);
    }
}
