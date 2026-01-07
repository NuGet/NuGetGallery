// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Status.Tests
{
    public class TestComponent : Component
    {
        public TestComponent(
            string name,
            bool displaySubComponents)
            : this(name, new IComponent[0], displaySubComponents)
        {
        }

        public TestComponent(
            string name,
            IEnumerable<IComponent> subComponents,
            bool displaySubComponents)
            : base(name, "", subComponents, displaySubComponents)
        {
        }

        public override ComponentStatus Status { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
