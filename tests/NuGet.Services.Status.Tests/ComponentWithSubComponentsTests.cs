// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Status.Tests
{
    public abstract class ComponentWithSubComponentsTests : ComponentTests
    {
        protected abstract IComponent CreateComponent(string name, string description, IEnumerable<IComponent> subComponents);
        protected abstract ComponentStatus GetExpectedStatusWithTwoSubComponents(ComponentStatus subStatus1, ComponentStatus subStatus2);

        [Fact]
        public void ThrowsIfNameNullWithSubComponents()
        {
            Assert.Throws<ArgumentNullException>(() => CreateComponent(null, "description", Enumerable.Empty<IComponent>()));
        }

        [Fact]
        public void GetByPathReturnsExistingSubComponents()
        {
            var innermostComponent = CreateComponent("innermostSubComponent", "innermostSubDescription");
            var middleComponent = CreateComponent("middleSubComponent", "middleSubDescription", new[] { innermostComponent });
            var rootComponent = CreateComponent("rootComponent", "rootDescription", new[] { middleComponent });
            
            AssertPath(rootComponent, rootComponent, rootComponent.Name);

            var middleSubComponent = rootComponent.SubComponents.First();
            AssertPath(rootComponent, middleSubComponent, rootComponent.Name, middleComponent.Name);

            var innermostSubComponent = rootComponent.SubComponents.First().SubComponents.First();
            AssertPath(rootComponent, innermostSubComponent, rootComponent.Name, middleComponent.Name, innermostComponent.Name);
        }

        private void AssertPath<TComponent>(TComponent root, TComponent subComponent, params string[] componentNames)
            where TComponent : class, IComponentDescription, IRootComponent<TComponent>
        {
            AssertUtility.AssertComponent(subComponent, root.GetByNames(componentNames));

            var expectedPath = ComponentUtility.GetPath(componentNames);
            Assert.Equal(expectedPath, subComponent.Path);
            AssertUtility.AssertComponent(subComponent, root.GetByPath(expectedPath));
        }

        [Fact]
        public void GetByPathReturnsNullForMissingSubComponents()
        {
            var innermostComponent = CreateComponent("innermostSubComponent", "innermostSubDescription");
            var middleComponent = CreateComponent("middleSubComponent", "middleSubDescription", new[] { innermostComponent });
            var rootComponent = CreateComponent("rootComponent", "rootDescription", new[] { middleComponent });

            Assert.Throws<ArgumentNullException>(() => ComponentUtility.GetPath((string[])null));

            Assert.Null(rootComponent.GetByPath(null));
            Assert.Null(rootComponent.GetByNames((string[])null));
            
            AssertMissingPath(rootComponent, (string)null);
            AssertMissingPath(rootComponent, "missingRootComponent");
            AssertMissingPath(rootComponent, "missingRootComponent", "withSubComponent");

            AssertMissingPath(rootComponent, rootComponent.Name, null);
            AssertMissingPath(rootComponent, rootComponent.Name, "missingSubComponent");
            AssertMissingPath(rootComponent, rootComponent.Name, "missingSubComponent", "withSubSubComponent");

            AssertMissingPath(rootComponent, rootComponent.Name, middleComponent.Name, null);
            AssertMissingPath(rootComponent, rootComponent.Name, middleComponent.Name, "missingSubSubComponent");
            AssertMissingPath(rootComponent, rootComponent.Name, middleComponent.Name, "missingSubSubComponent", "withSubSubSubComponent");
        }

        private void AssertMissingPath<TComponent>(TComponent root, params string[] componentNames)
            where TComponent : class, IComponentDescription, IRootComponent<TComponent>
        {
            Assert.Null(root.GetByNames(componentNames));

            var missingPath = ComponentUtility.GetPath(componentNames);
            Assert.Null(root.GetByPath(missingPath));
        }

        [Fact]
        public void ThrowsIfSubComponentsNull()
        {
            Assert.Throws<ArgumentNullException>(() => CreateComponent("name", "description", null));
        }

        [Theory]
        [ClassData(typeof(ComponentTestData.ComponentStatusPairs))]
        public void ReturnsStatusIfSetWithSubComponent(ComponentStatus status, ComponentStatus subStatus)
        {
            var subComponent = CreateComponent("subComponent", "subdescription");
            subComponent.Status = subStatus;

            var component = CreateComponent("component", "description", new[] { subComponent });
            component.Status = status;

            Assert.Equal(status, component.Status);
        }

        [Theory]
        [ClassData(typeof(ComponentTestData.ComponentStatusTriplets))]
        public void ReturnsStatusIfSetWithMultipleSubComponents(
            ComponentStatus status, 
            ComponentStatus subStatus1, 
            ComponentStatus subStatus2)
        {
            var subComponent1 = CreateComponent("subComponent1", "subdescription1");
            subComponent1.Status = subStatus1;

            var subComponent2 = CreateComponent("subComponent2", "subdescription2");
            subComponent2.Status = subStatus2;

            var component = CreateComponent("component", "description", new[] { subComponent1, subComponent2 });
            component.Status = status;

            Assert.Equal(status, component.Status);
        }

        [Theory]
        [ClassData(typeof(ComponentTestData.ComponentStatuses))]
        public void ReturnsSubStatusIfStatusNotSet(ComponentStatus subStatus)
        {
            var subComponent = CreateComponent("subComponent", "subdescription");
            subComponent.Status = subStatus;

            var component = CreateComponent("component", "description", new[] { subComponent });

            Assert.Equal(subStatus, component.Status);
        }

        [Theory]
        [ClassData(typeof(ComponentTestData.ComponentStatusPairs))]
        public void ReturnsSubStatusIfStatusNotSetWithTwoSubComponents(
            ComponentStatus subStatus1, 
            ComponentStatus subStatus2)
        {
            var subComponent1 = CreateComponent("subComponent1", "subdescription1");
            subComponent1.Status = subStatus1;

            var subComponent2 = CreateComponent("subComponent2", "subdescription2");
            subComponent2.Status = subStatus2;

            var component = CreateComponent("component", "description", new[] { subComponent1, subComponent2 });

            Assert.Equal(GetExpectedStatusWithTwoSubComponents(subStatus1, subStatus2), component.Status);
        }

        [Fact]
        public void LeastCommonAncestorPathReturnsEmptyIfNoCommonAncestor()
        {
            var component1 = CreateComponent("component1", "description1");
            var component2 = CreateComponent("component2", "description2");
            AssertLeastCommonAncestorPath(component1, component2, "");
        }

        [Fact]
        public void LeastCommonAncestorPathReturnsIntermediatePath()
        {
            var subComponent1 = CreateComponent("subComponent1", "subDescription1");
            var subComponent2 = CreateComponent("subComponent2", "subDescription2");
            var ancestorComponent = CreateComponent("root", "rootDescription", new[] { subComponent1, subComponent2 });

            AssertLeastCommonAncestorPath(
                ancestorComponent.SubComponents.Single(c => c.Name == subComponent1.Name),
                ancestorComponent.SubComponents.Single(c => c.Name == subComponent2.Name),
                ancestorComponent.Path);
        }

        [Fact]
        public void LeastCommonAncestorPathReturnsIdenticalComponent()
        {
            var component = CreateComponent("component", "description");
            AssertLeastCommonAncestorPath(component, component, component.Path);
        }

        [Fact]
        public void LeastCommonAncestorPathReturnsAncestor()
        {
            var subComponent = CreateComponent("subComponent", "subDescription");
            var ancestorComponent = CreateComponent("root", "rootDescription", new[] { subComponent });
            AssertLeastCommonAncestorPath(
                ancestorComponent.SubComponents.Single(c => c.Name == subComponent.Name), 
                ancestorComponent, 
                ancestorComponent.Path);
        }
        
        [Theory]
        [ClassData(typeof(ComponentTestData.ComponentStatusPairs))]
        public void SetsDisplaySubComponentsProperly(
            ComponentStatus subStatus1,
            ComponentStatus subStatus2)
        {
            var subComponent1 = CreateComponent("subComponent1", "subDescription1");
            subComponent1.Status = subStatus1;

            var subComponent2 = CreateComponent("subComponent2", "subDescription2");
            subComponent2.Status = subStatus2;

            var ancestorComponent = CreateComponent("root", "rootDescription", new[] { subComponent1, subComponent2 });

            AssertDisplayComponentsSetProperly(ancestorComponent);
        }

        /// <summary>
        /// <see cref="Component.DisplaySubComponents"/> should be set to false by a <see cref="Component"/> implementation if 
        /// when one of its <see cref="Component.SubComponents"/> is not <see cref="Component.Up"/>, it MUST NOT return <see cref="ComponentStatus.Up"/>.
        /// </summary>
        /// <remarks>
        /// The intention of <see cref="Component.DisplaySubComponents"/> is to hide components that are not impactful from being displayed to customers.
        /// If a component determines status in such a way that it is not degraded when any of its subcomponents are degraded, 
        /// it implies that its subcomponents are not impactful, so it should set <see cref="Component.DisplaySubComponents"/> to false.
        /// </remarks>
        private void AssertDisplayComponentsSetProperly(IComponent ancestorComponent)
        {
            Assert.True(
                // Either all subcomponents are up
                ancestorComponent.SubComponents.All(c => c.Status == ComponentStatus.Up) ||
                // Or the status is not up
                ancestorComponent.Status != ComponentStatus.Up || 
                // Or we are hiding subcomponents
                !ancestorComponent.DisplaySubComponents);
        }

        private void AssertLeastCommonAncestorPath(IComponent firstComponent, IComponent secondComponent, string expectedLeastCommonAncestor)
        {
            AssertLeastCommonAncestorPathHelper(firstComponent, secondComponent, expectedLeastCommonAncestor);
            AssertLeastCommonAncestorPathHelper(secondComponent, firstComponent, expectedLeastCommonAncestor);
        }

        private void AssertLeastCommonAncestorPathHelper(IComponent firstComponent, IComponent secondComponent, string expectedLeastCommonAncestor)
        {
            Assert.Equal(expectedLeastCommonAncestor, ComponentUtility.GetLeastCommonAncestorPath(firstComponent, secondComponent));
            Assert.Equal(expectedLeastCommonAncestor, ComponentUtility.GetLeastCommonAncestorPath(firstComponent.Path, secondComponent.Path));
        }
    }
}
