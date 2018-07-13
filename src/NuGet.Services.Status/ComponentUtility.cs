// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status
{
    public static class ComponentUtility
    {
        /// <summary>
        /// Concatenates a set of <see cref="IReadOnlyComponent.Name"/>s into a <see cref="IReadOnlyComponent.Path"/>.
        /// </summary>
        public static string GetPath(params string[] componentNames)
        {
            componentNames = componentNames ?? throw new ArgumentNullException(nameof(componentNames));

            return string.Join(Constants.ComponentPathDivider.ToString(), componentNames);
        }

        /// <summary>
        /// Gets the subcomponent of <paramref name="component"/> with <see cref="IReadOnlyComponent.Path"/> <paramref name="path"/>.
        /// If none exists, returns <c>null</c>.
        /// </summary>
        public static TComponent GetByPath<TComponent>(this TComponent component, string path)
            where TComponent : class, IReadOnlyComponent, IRootComponent<TComponent>
        {
            if (path == null)
            {
                return null;
            }

            var componentNames = path.Split(Constants.ComponentPathDivider);
            return component.GetByNames(componentNames);
        }

        /// <summary>
        /// Gets the subcomponent of <paramref name="component"/> by iterating its subcomponents in the order of <paramref name="componentNames"/>.
        /// If none exists, returns <c>null</c>.
        /// </summary>
        public static TComponent GetByNames<TComponent>(this TComponent component, params string[] componentNames)
            where TComponent : class, IReadOnlyComponent, IRootComponent<TComponent>
        {
            if (component == null)
            {
                return null;
            }

            if (componentNames == null)
            {
                return null;
            }

            TComponent currentComponent = null;
            IRootComponent<TComponent> currentRoot = new GetByNamesHelper<TComponent>(component);
            foreach (var componentName in componentNames)
            {
                currentComponent = currentRoot.SubComponents.FirstOrDefault(c => c.Name == componentName);

                if (currentComponent == null)
                {
                    break;
                }

                currentRoot = currentComponent;
            }

            return currentComponent;
        }

        /// <remarks>
        /// Dummy implementation of <see cref="IRootComponent{TComponent}"/> used by <see cref="GetByNames{TComponent}(TComponent, string[])"/>.
        /// </remarks>
        private class GetByNamesHelper<TComponent> : IRootComponent<TComponent>
            where TComponent : IReadOnlyComponent
        {
            public IEnumerable<TComponent> SubComponents { get; }

            public GetByNamesHelper(TComponent root)
            {
                SubComponents = new[] { root };
            }
        }
    }
}
