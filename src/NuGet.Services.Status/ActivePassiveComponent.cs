// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Status
{
    /// <summary>
    /// An <see cref="IComponent"/> with an active component that ignores the status of its passive subcomponents if its active subcomponent is up.
    /// </summary>
    /// <remarks>
    /// The active subcomponent is the first subcomponent in its <see cref="Component.SubComponents"/>. All other subcomponents are passive.
    /// </remarks>
    /// <example>
    /// A website is deployed to two regions, region A and region B.
    /// If region A is performing as expected, traffic is directed to region A.
    /// If region A is not performing as expected, traffic is directed to region B.
    /// Customers will never see an impact as long as region A is up, even if region B is completely down.
    /// </example>
    public class ActivePassiveComponent : Component
    {
        public ActivePassiveComponent(
            string name,
            string description)
            : base(name, description)
        {
        }

        public ActivePassiveComponent(
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
                
                // Iterate through the list of subcomponents in order.
                var isFirst = true;
                foreach (var subComponent in SubComponents)
                {
                    if (subComponent.Status == ComponentStatus.Up)
                    {
                        // If the first component is up, the status is up.
                        // If any child component is up but the first component is not up, the status is degraded.
                        return isFirst ? ComponentStatus.Up : ComponentStatus.Degraded;
                    }

                    // If any component is degraded, the status is degraded.
                    if (subComponent.Status == ComponentStatus.Degraded)
                    {
                        return ComponentStatus.Degraded;
                    }

                    isFirst = false;
                }

                // If all components are down, the status is down.
                return isFirst ? ComponentStatus.Up : ComponentStatus.Down;
            }
            set
            {
                _status = value;
            }
        }
    }
}
