// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status
{
    /// <summary>
    /// An <see cref="IComponent"/> that returns the best status of any of its <see cref="IReadOnlyComponent.SubComponents"/>.
    /// </summary>
    /// <example>
    /// A website is deployed to two regions, region A and region B.
    /// Traffic is split between any regions that are healthy.
    /// Customers will never see an impact as long as either region A or region B are healthy.
    /// </example>
    public class ActiveActiveComponent : Component
    {
        public ActiveActiveComponent(
            string name,
            string description)
            : base(name, description)
        {
        }

        public ActiveActiveComponent(
            string name,
            string description,
            IEnumerable<IComponent> subComponents)
            : base(name, description, subComponents, displaySubComponents: false)
        {
        }

        private ComponentStatus? _status = null;
        public override ComponentStatus Status
        {
            get
            {
                if (_status.HasValue)
                {
                    return _status.Value;
                }

                if (!SubComponents.Any())
                {
                    return ComponentStatus.Up;
                }

                return SubComponents.Min(s => s.Status);
            }
            set
            {
                _status = value;
            }
        }
    }
}
