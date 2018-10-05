// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Status
{
    /// <summary>
    /// A part of the service that has a set of subcomponents of type <typeparamref name="TComponent"/>.
    /// </summary>
    /// <remarks>
    /// This interface exists for the sole purpose of making the implementation of 
    /// <see cref="ComponentUtility.GetByPath{TComponent}(TComponent, string)"/> and 
    /// <see cref="ComponentUtility.GetByNames{TComponent}(TComponent, string[])"/> 
    /// reusable between <see cref="IReadOnlyComponent"/> and <see cref="IComponent"/>.
    /// </remarks>
    public interface IRootComponent<out TComponent> 
        where TComponent : IComponentDescription
    {
        /// <summary>
        /// A list of subcomponents that make up this part of the service.
        /// </summary>
        IEnumerable<TComponent> SubComponents { get; }
    }
}
