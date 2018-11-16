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
    public abstract class Component : IComponent
    {
        public string Name { get; }
        public string Description { get; }
        public abstract ComponentStatus Status { get; set; }
        public IEnumerable<IComponent> SubComponents { get; }
        public string Path => Name;
        public bool DisplaySubComponents { get; }

        public Component(
            string name,
            string description)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description;
            SubComponents = Enumerable.Empty<IComponent>();
            DisplaySubComponents = false;
        }

        public Component(
            string name,
            string description,
            IEnumerable<IComponent> subComponents,
            bool displaySubComponents)
            : this(name, description)
        {
            SubComponents = subComponents?.Select(s => new ComponentWrapper(s, this)).ToList()
                ?? throw new ArgumentNullException(nameof(subComponents));

            DisplaySubComponents = displaySubComponents;
        }
    }
}
