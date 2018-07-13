// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status
{
    /// <summary>
    /// Default implementation of <see cref="IComponent"/>.
    /// </summary>
    /// <remarks>
    /// An <see cref="IComponent"/> should calculate its <see cref="Status"/> based on its <see cref="SubComponents"/>.
    /// Different subclasses of <see cref="Component"/> should calculate their <see cref="Status"/> differently.
    /// </remarks>
    public abstract class Component : ReadOnlyComponent, IComponent
    {
        public new abstract ComponentStatus Status { get; set; }
        public new IEnumerable<IComponent> SubComponents { get; }

        public Component(
            string name,
            string description)
            : base(name, description)
        {
            SubComponents = Enumerable.Empty<IComponent>();
        }

        public Component(
            string name,
            string description,
            IEnumerable<IComponent> subComponents)
            : base(name, description, subComponents)
        {
            SubComponents = subComponents?.Select(s => new ComponentWrapper(s, this)).ToList()
                ?? throw new ArgumentNullException(nameof(subComponents));
        }
    }
}
