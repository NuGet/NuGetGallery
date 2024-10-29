// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Moq;

namespace NuGetGallery.Framework
{
    // used to set properties of Mock objects from the container - black magic ahead
    public static class MockWiringExtensions
    {
        public static void InjectMockProperties<T>(this IComponentContext context, Mock<T> mock)
            where T : class
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (mock == null)
                throw new ArgumentNullException(nameof(mock));

            object instance = mock.Object;

            foreach (PropertyInfo propertyInfo in instance.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(pi => pi.CanWrite))
            {
                Type propertyType = propertyInfo.PropertyType;
                if (!propertyType.Namespace.StartsWith("System.") && context.IsRegistered(propertyType))
                {
                    MethodInfo[] accessors = propertyInfo.GetAccessors(false);
                    if (accessors.Length > 0 && accessors[0].ReturnType != typeof(void) && !accessors[0].ReturnType.IsValueType)
                    {
                        object obj = context.Resolve(propertyType);
                        propertyInfo.SetValue(instance, obj, null);
                    }
                }
            }
        }
    }
}