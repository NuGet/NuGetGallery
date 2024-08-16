// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Status.Tests
{
    public class GetAllComponentsTests
    {
        [Fact]
        public void ReturnsEmptyForNullComponent()
        {
            Assert.Empty(ComponentUtility.GetAllVisibleComponents(null));
        }

        [Fact]
        public void ReturnsEmptyForNullReadOnlyComponent()
        {
            Assert.Empty(ComponentUtility.GetAllComponents(null));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnsLeafComponent(bool displaySubComponents)
        {
            var component = CreateDummyComponent(displaySubComponents);

            var result = ComponentUtility.GetAllVisibleComponents(component);

            AssertContains(result, new[] { component });
        }

        [Fact]
        public void ReturnsLeafReadOnlyComponent()
        {
            var component = CreateDummyReadOnlyComponent();

            var result = ComponentUtility.GetAllComponents(component);

            AssertContains(result, new[] { component });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ReturnsComponentAndSubcomponents(bool displaySubComponents, bool displaySubSubComponents)
        {
            var subComponent1 = CreateDummyComponent(displaySubSubComponents);
            var subComponent2 = CreateDummyComponent(displaySubSubComponents);
            var component = CreateDummyComponent(new[] { subComponent1, subComponent2 }, displaySubComponents);

            var result = ComponentUtility.GetAllVisibleComponents(component);

            AssertContains(result, 
                new[] { component }
                    .Concat(displaySubComponents ? component.SubComponents : new IComponent[0]));
        }

        [Fact]
        public void ReturnsReadOnlyComponentAndSubcomponents()
        {
            var subComponent1 = CreateDummyReadOnlyComponent();
            var subComponent2 = CreateDummyReadOnlyComponent();
            var component = CreateDummyReadOnlyComponent(new[] { subComponent1, subComponent2 });

            var result = ComponentUtility.GetAllComponents(component);

            AssertContains(result, 
                new[] { component }
                    .Concat(component.SubComponents));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public void ReturnsComponentAndSubcomponentsAndSubsubcomponents(bool displaySubComponents, bool displaySubSubComponents, bool displaySubSubSubComponents)
        {
            var sub1SubComponent1 = CreateDummyComponent(displaySubSubSubComponents);
            var sub1SubComponent2 = CreateDummyComponent(displaySubSubSubComponents);
            var subComponent1 = CreateDummyComponent(new[] { sub1SubComponent1, sub1SubComponent2 }, displaySubSubComponents);

            var sub2SubComponent1 = CreateDummyComponent(displaySubSubSubComponents);
            var sub2SubComponent2 = CreateDummyComponent(displaySubSubSubComponents);
            var subComponent2 = CreateDummyComponent(new[] { sub2SubComponent1, sub2SubComponent2 }, displaySubSubComponents);

            var component = CreateDummyComponent(new[] { subComponent1, subComponent2 }, displaySubComponents);

            var result = ComponentUtility.GetAllVisibleComponents(component);

            AssertContains(result, 
                new[] { component }
                    .Concat(displaySubComponents ? component.SubComponents : new IComponent[0])
                    .Concat(displaySubComponents && displaySubSubComponents ? component.SubComponents.First().SubComponents : new IComponent[0])
                    .Concat(displaySubComponents && displaySubSubComponents ? component.SubComponents.Last().SubComponents : new IComponent[0]));
        }

        [Fact]
        public void ReturnsReadOnlyComponentAndSubcomponentsAndSubsubcomponents()
        {
            var sub1SubComponent1 = CreateDummyReadOnlyComponent();
            var sub1SubComponent2 = CreateDummyReadOnlyComponent();
            var subComponent1 = CreateDummyReadOnlyComponent(new[] { sub1SubComponent1, sub1SubComponent2 });

            var sub2SubComponent1 = CreateDummyReadOnlyComponent();
            var sub2SubComponent2 = CreateDummyReadOnlyComponent();
            var subComponent2 = CreateDummyReadOnlyComponent(new[] { sub2SubComponent1, sub2SubComponent2 });

            var component = CreateDummyReadOnlyComponent(new[] { subComponent1, subComponent2 });

            var result = ComponentUtility.GetAllComponents(component);

            AssertContains(result,
                new[] { component }
                    .Concat(component.SubComponents)
                    .Concat(component.SubComponents.First().SubComponents)
                    .Concat(component.SubComponents.Last().SubComponents));
        }

        private void AssertContains(IEnumerable<object> array, IEnumerable<object> contents)
        {
            Assert.Equal(contents.Count(), array.Count());
            foreach (var content in contents)
            {
                Assert.Contains(content, array);
            }
        }

        private IComponent CreateDummyComponent(bool displaySubComponents)
        {
            return CreateDummyComponent(Enumerable.Empty<IComponent>(), displaySubComponents);
        }

        private IComponent CreateDummyComponent(IEnumerable<IComponent> subcomponents, bool displaySubComponents)
        {
            return new TestComponent("", subcomponents, displaySubComponents);
        }

        private IReadOnlyComponent CreateDummyReadOnlyComponent()
        {
            return CreateDummyReadOnlyComponent(Enumerable.Empty<IReadOnlyComponent>());
        }

        private IReadOnlyComponent CreateDummyReadOnlyComponent(IEnumerable<IReadOnlyComponent> subcomponents)
        {
            return new ReadOnlyComponent("", "", ComponentStatus.Up, subcomponents);
        }
    }
}
