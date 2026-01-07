// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status
{
    public class ReadOnlyComponent : IReadOnlyComponent
    {
        public string Name { get; }
        public string Description { get; }
        public ComponentStatus Status { get; }
        public IEnumerable<IReadOnlyComponent> SubComponents { get; }
        [JsonIgnore]
        public string Path => Name;

        protected ReadOnlyComponent(
            string name,
            string description)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description;
        }

        protected ReadOnlyComponent(
            string name,
            string description,
            IEnumerable<IReadOnlyComponent> subComponents)
            : this(name, description)
        {
            SubComponents = subComponents?.Select(s => new ReadOnlyComponentWrapper(s, this)).ToList()
                ?? Enumerable.Empty<IReadOnlyComponent>();
        }

        public ReadOnlyComponent(
            string name,
            string description,
            ComponentStatus status,
            IEnumerable<IReadOnlyComponent> subComponents)
            : this(name, description, subComponents)
        {
            Status = status;
        }

        [JsonConstructor]
        public ReadOnlyComponent(
            string name, 
            string description, 
            ComponentStatus status, 
            IEnumerable<ReadOnlyComponent> subComponents)
            : this(name, description, status, subComponents?.Cast<IReadOnlyComponent>())
        {
        }

        public ReadOnlyComponent(IComponent component)
            : this(
                  component.Name, 
                  component.Description, 
                  component.Status, 
                  component.DisplaySubComponents 
                    ? component.SubComponents.Select(s => new ReadOnlyComponent(s)).ToList() 
                    : Enumerable.Empty<IReadOnlyComponent>())
        {
        }
    }
}
