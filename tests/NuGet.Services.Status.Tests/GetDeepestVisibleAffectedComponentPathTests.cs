// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Xunit;

namespace NuGet.Services.Status.Tests
{
    public class GetDeepestVisibleAffectedComponentPathTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnsSubComponentIfPathHasNoParts(bool displaySubComponents)
        {
            var component = new TestComponent("", displaySubComponents);
            AssertDeepestVisibleAncestor(component, component, component);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnsRootComponent(bool displaySubComponents)
        {
            var component = new TestComponent("path", displaySubComponents);
            AssertDeepestVisibleAncestor(component, component, component);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ReturnsNullIfNotASubComponent(bool displaySubComponentsRoot, bool displaySubComponentsSubComponent)
        {
            var root = new TestComponent("root", displaySubComponentsRoot);
            var subComponent = new TestComponent("notInTree", displaySubComponentsSubComponent);
            AssertDeepestVisibleAncestor(root, subComponent, null);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnsSubComponent(bool displaySubComponents)
        {
            var subComponent = new TestComponent("child", displaySubComponents);
            var root = new TestComponent("root", new[] { subComponent }, displaySubComponents: true);
            AssertDeepestVisibleAncestor(
                root,
                root.SubComponents.Single(c => c.Name == subComponent.Name),
                root.SubComponents.Single(c => c.Name == subComponent.Name));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnsIntermediateSubComponent(bool displaySubComponents)
        {
            var subComponent = new TestComponent("child", displaySubComponents);
            var intermediateComponent = new TestComponent("middle", new[] { subComponent }, displaySubComponents: false);
            var root = new TestComponent("root", new[] { intermediateComponent }, displaySubComponents: true);
            AssertDeepestVisibleAncestor(
                root,
                root.SubComponents.Single(c => c.Name == intermediateComponent.Name).SubComponents.Single(c => c.Name == subComponent.Name),
                root.SubComponents.Single(c => c.Name == intermediateComponent.Name));
        }

        private void AssertDeepestVisibleAncestor(IComponent root, IComponent subComponent, IComponent expected)
        {
            Assert.Equal(expected, root.GetDeepestVisibleAncestorOfSubComponent(subComponent));
        }
    }
}
