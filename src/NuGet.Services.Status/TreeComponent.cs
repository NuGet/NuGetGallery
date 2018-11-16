// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status
{
    /// <summary>
    /// A <see cref="Component"/> that treats all of its <see cref="Component.SubComponents"/> equally.
    /// </summary>
    /// <example>
    /// A website is deployed to two regions, region A and region B.
    /// Region A handles all traffic from users near it.
    /// Region B handles all traffic from users near it.
    /// If either region A or region B are down, some customers are impacted.
    /// </example>
    public class TreeComponent : Component
    {
        public TreeComponent(
            string name,
            string description)
            : base(name, description)
        {
        }

        public TreeComponent(
            string name,
            string description,
            IEnumerable<IComponent> subComponents)
            : base(name, description, subComponents, displaySubComponents: true)
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
                
                // If all subcomponents are up, we are up.
                if (SubComponents.All(c => c.Status == ComponentStatus.Up))
                {
                    return ComponentStatus.Up;
                }

                // If all subcomponents are down, we are down.
                if (SubComponents.All(c => c.Status == ComponentStatus.Down))
                {
                    return ComponentStatus.Down;
                }

                // Otherwise, we are degraded, because some subcomponents are degraded or down but not all.
                return ComponentStatus.Degraded;
            }
            set
            {
                _status = value;
            }
        }
    }
}
