// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Status;

namespace StatusAggregator.Tests
{
    public class TestComponent : Component
    {
        private const string DefaultDescription = "";

        public TestComponent(string name)
            : base(name, DefaultDescription)
        {
        }

        public TestComponent(
            string name,
            IEnumerable<IComponent> subComponents,
            bool displaySubComponents = true)
            : base(name, DefaultDescription, subComponents, displaySubComponents)
        {
        }

        public override ComponentStatus Status { get; set; }
    }
}