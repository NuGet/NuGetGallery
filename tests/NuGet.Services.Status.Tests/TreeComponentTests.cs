// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Status.Tests
{
    public class TreeComponentTests : ComponentWithSubComponentsTests
    {
        protected override IComponent CreateComponent(string name, string description)
        {
            return new TreeComponent(name, description);
        }

        protected override IComponent CreateComponent(string name, string description, IEnumerable<IComponent> subComponents)
        {
            return new TreeComponent(name, description, subComponents);
        }

        protected override ComponentStatus GetExpectedStatusWithTwoSubComponents(ComponentStatus subStatus1, ComponentStatus subStatus2)
        {
            if (subStatus1 == ComponentStatus.Up && subStatus2 == ComponentStatus.Up)
            {
                return ComponentStatus.Up;
            }

            if (subStatus1 == ComponentStatus.Down && subStatus2 == ComponentStatus.Down)
            {
                return ComponentStatus.Down;
            }

            return ComponentStatus.Degraded;
        }
    }
}
