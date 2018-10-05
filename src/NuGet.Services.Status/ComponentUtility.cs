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
        /// Concatenates a set of <see cref="IComponentDescription.Name"/>s into a <see cref="IComponentDescription.Path"/>.
        /// </summary>
        public static string GetPath(params string[] componentNames)
        {
            componentNames = componentNames ?? throw new ArgumentNullException(nameof(componentNames));

            return string.Join(Constants.ComponentPathDivider.ToString(), componentNames);
        }

        public static string[] GetNames(this IComponentDescription component)
        {
            return GetNames(component.Path);
        }

        public static string[] GetNames(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new string[0];
            }

            return path.Split(Constants.ComponentPathDivider);
        }

        /// <summary>
        /// Gets the subcomponent of <paramref name="component"/> with <see cref="IComponentDescription.Path"/> <paramref name="path"/>.
        /// If none exists, returns <c>null</c>.
        /// </summary>
        public static TComponent GetByPath<TComponent>(this TComponent component, string path)
            where TComponent : class, IComponentDescription, IRootComponent<TComponent>
        {
            if (path == null)
            {
                return null;
            }

            var componentNames = GetNames(path);
            return component.GetByNames(componentNames);
        }

        /// <summary>
        /// Gets the subcomponent of <paramref name="component"/> by iterating its subcomponents in the order of <paramref name="componentNames"/>.
        /// If none exists, returns <c>null</c>.
        /// </summary>
        public static TComponent GetByNames<TComponent>(this TComponent component, params string[] componentNames)
            where TComponent : class, IComponentDescription, IRootComponent<TComponent>
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
            where TComponent : IComponentDescription
        {
            public IEnumerable<TComponent> SubComponents { get; }

            public GetByNamesHelper(TComponent root)
            {
                SubComponents = new[] { root };
            }
        }

        /// <summary>
        /// Recursively iterates through the subcomponents of <paramref name="component"/> and returns all components found, including <paramref name="component"/> itself.
        /// </summary>
        public static IEnumerable<IReadOnlyComponent> GetAllComponents(this IReadOnlyComponent component)
        {
            if (component == null)
            {
                yield break;
            }

            yield return component;

            foreach (var subcomponent in component.SubComponents.SelectMany(s => s.GetAllComponents()))
            {
                yield return subcomponent;
            }
        }

        /// <summary>
        /// Recursively iterates through the subcomponents of <paramref name="component"/> and returns all components found, including <paramref name="component"/> itself.
        /// </summary>
        public static IEnumerable<IComponent> GetAllVisibleComponents(this IComponent component)
        {
            if (component == null)
            {
                yield break;
            }

            yield return component;

            if (!component.DisplaySubComponents)
            {
                yield break;
            }

            foreach (var subcomponent in component.SubComponents.SelectMany(s => s.GetAllVisibleComponents()))
            {
                yield return subcomponent;
            }
        }

        /// <summary>
        /// Returns the deepest common ancestor of <paramref name="firstComponent"/> and <paramref name="secondComponent"/>.
        /// </summary>
        public static string GetLeastCommonAncestorPath<TComponent>(TComponent firstComponent, TComponent secondComponent)
            where TComponent : class, IComponentDescription, IRootComponent<TComponent>
        {
            return GetLeastCommonAncestorPath(firstComponent.Path, secondComponent.Path);
        }

        /// <summary>
        /// Returns the deepest common ancestor of <paramref name="firstPath"/> and <paramref name="secondPath"/>.
        /// </summary>
        public static string GetLeastCommonAncestorPath(string firstPath, string secondPath)
        {
            var firstPathParts = GetNames(firstPath);
            var secondPathParts = GetNames(secondPath);

            var maxPotentialCommonPathParts = new[] { firstPathParts, secondPathParts }.Min(p => p.Length);

            // Iterate through the two paths until we find an ancestor that is different.
            int commonPathParts;
            for (commonPathParts = 0; commonPathParts < maxPotentialCommonPathParts; commonPathParts++)
            {
                if (firstPathParts[commonPathParts] != secondPathParts[commonPathParts])
                {
                    // If the paths differ at depth N, then there are N common nodes.
                    break;
                }
            }

            return GetPath(firstPathParts.Take(commonPathParts).ToArray());
        }

        /// <summary>
        /// Returns the deepest ancestor of <paramref name="subComponent"/> that is visible (does not have a parent with <see cref="IComponent.DisplaySubComponents"/> set to <c>false</c>).
        /// </summary>
        public static IComponent GetDeepestVisibleAncestorOfSubComponent(this IComponent rootComponent, IComponent subComponent)
        {
            var pathParts = subComponent.GetNames();
            for (var i = 1; i <= pathParts.Length; i++)
            {
                var path = GetPath(pathParts.Take(i).ToArray());
                var component = rootComponent.GetByPath(path);
                if (component == null)
                {
                    // The subcomponent must not be a part of the component tree, so it has no least common visible ancestor.
                    return null;
                }

                if (!component.DisplaySubComponents)
                {
                    // If a component has hidden its subcomponents, then it is the least common visible ancestor.
                    return component;
                }
            }

            // If we can successfully travel to the subcomponent without finding a component with hidden subcomponents, then the subcomponent itself must be the deepest.
            return subComponent;
        }

        /// <summary>
        /// Returns the path of <paramref name="child"/> as a subcomponent of <paramref name="parent"/>.
        /// </summary>
        internal static string GetSubPath(IComponentDescription parent, IComponentDescription child)
        {
            return parent.Path + Constants.ComponentPathDivider + child.Name;
        }
    }
}
