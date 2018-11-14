// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Status.Tests
{
    public static class AssertUtility
    {
        public static void AssertStatus(ServiceStatus expected, ServiceStatus actual)
        {
            AssertFieldEqual(expected, actual, i => i.LastBuilt);
            AssertFieldEqual(expected, actual, i => i.LastUpdated);
            AssertFieldEqual(expected, actual, i => i.ServiceRootComponent, AssertComponent);
            AssertFieldEqual(expected, actual, i => i.Events, AssertEvent);
        }

        public static void AssertComponent<TComponent>(TComponent expected, TComponent actual)
            where TComponent : class, IComponentDescription, IRootComponent<TComponent>
        {
            AssertFieldEqual(expected, actual, i => i.Description);
            AssertFieldEqual(expected, actual, i => i.Name);
            AssertFieldEqual(expected, actual, i => i.Status);
            AssertFieldEqual(expected, actual, i => i.SubComponents, AssertComponent<TComponent>);
            AssertFieldEqual(expected, actual, i => i.Path);
        }

        public static void AssertEvent(Event expected, Event actual)
        {
            AssertFieldEqual(expected, actual, i => i.AffectedComponentPath);
            AssertFieldEqual(expected, actual, i => i.EndTime);
            AssertFieldEqual(expected, actual, i => i.StartTime);
            AssertFieldEqual(expected, actual, i => i.Messages, AssertMessage);
        }

        public static void AssertMessage(Message expected, Message actual)
        {
            AssertFieldEqual(expected, actual, i => i.Contents);
            AssertFieldEqual(expected, actual, i => i.Time);
        }

        public static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, TField> accessor)
        {
            Assert.Equal(accessor(expected), accessor(actual));
        }

        public static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, TField> accessor,
            Action<TField, TField> assert)
        {
            assert(accessor(expected), accessor(actual));
        }

        public static void AssertFieldEqual<TParent, TField>(
            TParent expected,
            TParent actual,
            Func<TParent, IEnumerable<TField>> accessor,
            Action<TField, TField> assert)
        {
            AssertAll(accessor(expected), accessor(actual), assert);
        }

        public static void AssertAll<T>(IEnumerable<T> expecteds, IEnumerable<T> actuals, Action<T, T> assert)
        {
            if (expecteds == null)
            {
                Assert.Null(actuals);

                return;
            }

            Assert.Equal(expecteds.Count(), actuals.Count());
            var expectedsArray = expecteds.ToArray();
            var actualsArray = actuals.ToArray();
            for (int i = 0; i < expecteds.Count(); i++)
            {
                assert(expectedsArray[i], actualsArray[i]);
            }
        }
    }
}
