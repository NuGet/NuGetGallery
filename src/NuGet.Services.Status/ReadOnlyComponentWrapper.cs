// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status
{
    /// <summary>
    /// Wrapper class for <see cref="IReadOnlyComponent"/> that sets <see cref="IReadOnlyComponent.Path"/> as expected.
    /// </summary>
    /// <remarks>
    /// If we don't wrap or clone the subcomponents of a component, it wouldn't be possible to reuse the same component in multiple component trees.
    /// Although reusing a component in multiple component trees is an unlikely scenario, it would be difficult to disallow it with this design.
    /// As a result, a component only has a path when accessed as a subcomponent of another component.
    /// </remarks>
    internal class ReadOnlyComponentWrapper : IReadOnlyComponent
    {
        private readonly IReadOnlyComponent _component;
        private readonly IReadOnlyComponent _parent;

        public string Name => _component.Name;
        public string Description => _component.Description;
        public ComponentStatus Status => _component.Status;
        public IEnumerable<IReadOnlyComponent> SubComponents { get; }
        public string Path => ComponentUtility.GetSubPath(_parent, _component);

        public ReadOnlyComponentWrapper(IReadOnlyComponent component, IReadOnlyComponent parent)
        {
            _component = component;
            _parent = parent;
            SubComponents = _component.SubComponents?.Select(s => new ReadOnlyComponentWrapper(s, this)).ToList()
                ?? Enumerable.Empty<IReadOnlyComponent>();
        }
    }
}
