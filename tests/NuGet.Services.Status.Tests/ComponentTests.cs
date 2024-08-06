// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.Status.Tests
{
    public abstract class ComponentTests
    {
        protected abstract IComponent CreateComponent(string name, string description);

        [Fact]
        public void ThrowsIfNameNull()
        {
            Assert.Throws<ArgumentNullException>(() => CreateComponent(null, "description"));
        }

        [Fact]
        public void SucceedsWithoutSubComponents()
        {
            var name = "name";
            var description = "description";
            var component = CreateComponent(name, description);

            Assert.Equal(name, component.Name);
            Assert.Equal(description, component.Description);
            Assert.Equal(ComponentStatus.Up, component.Status);
            Assert.Empty(component.SubComponents);
            Assert.Equal(name, component.Path);
        }

        [Theory]
        [ClassData(typeof(ComponentTestData.ComponentStatuses))]
        public void ReturnsStatusIfSetWithoutSubComponents(ComponentStatus status)
        {
            var component = CreateComponent("component", "description");
            component.Status = status;

            Assert.Equal(status, component.Status);
        }
    }
}
