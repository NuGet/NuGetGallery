// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status
{
    /// <summary>
    /// Wrapper class for <see cref="IComponent"/> that sets <see cref="IComponent.Path"/> as expected.
    /// </summary>
    internal class ComponentWrapper : IComponent
    {
        private readonly IComponent _component;
        private readonly IComponent _parent;

        public string Name => _component.Name;
        public string Description => _component.Description;
        public ComponentStatus Status { get { return _component.Status; } set { _component.Status = value; } }
        public IEnumerable<IComponent> SubComponents { get; }
        public bool DisplaySubComponents => _component.DisplaySubComponents;
        public string Path => ComponentUtility.GetSubPath(_parent, _component);

        public ComponentWrapper(IComponent component, IComponent parent)
        {
            _component = component;
            _parent = parent;
            SubComponents = _component.SubComponents?.Select(s => new ComponentWrapper(s, this)).ToList()
                ?? Enumerable.Empty<IComponent>();
        }
    }
}
