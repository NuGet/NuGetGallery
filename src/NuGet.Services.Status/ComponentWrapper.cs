// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status
{
    /// <summary>
    /// Wrapper class for <see cref="IComponent"/> that sets <see cref="IComponent.Path"/> as expected.
    /// </summary>
    internal class ComponentWrapper : ReadOnlyComponentWrapper, IComponent
    {
        private readonly IComponent _component;
        private readonly IComponent _parent;

        public new ComponentStatus Status { get { return _component.Status; } set { _component.Status = value; } }
        public new IEnumerable<IComponent> SubComponents { get; }

        public ComponentWrapper(IComponent component, IComponent parent)
            : base(component, parent)
        {
            _component = component;
            _parent = parent;
            SubComponents = _component.SubComponents?.Select(s => new ComponentWrapper(s, this)).ToList()
                ?? Enumerable.Empty<IComponent>();
        }
    }
}
