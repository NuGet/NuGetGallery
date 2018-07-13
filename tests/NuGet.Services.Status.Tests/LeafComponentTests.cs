// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status.Tests
{
    public class LeafComponentTests : ComponentTests
    {
        protected override IComponent CreateComponent(string name, string description)
        {
            return new LeafComponent(name, description);
        }
    }
}
