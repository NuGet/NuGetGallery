// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Factory;
using StatusAggregator.Parse;
using Xunit;

namespace StatusAggregator.Tests.Factory
{
    public class EventAffectedComponentPathProviderTests
    {
        public class TheGetMethod
            : EventAffectedComponentPathProviderTest
        {
            public static IEnumerable<object[]> GetsAffectedComponentPath_Data
            {
                get
                {
                    var root = new NuGetServiceComponentFactory().Create();

                    yield return new object[] { root.Path, root.Path };

                    foreach (var subcomponent in root.SubComponents)
                    {
                        foreach (var subsubcomponent in GetAllComponents(subcomponent))
                        {
                            yield return new object[] { subcomponent.Path, subsubcomponent.Path };
                        }
                    }
                }
            }

            private static IEnumerable<IComponent> GetAllComponents(IComponent component)
            {
                if (component == null)
                {
                    yield break;
                }

                yield return component;

                foreach (var subcomponent in component.SubComponents.SelectMany(s => GetAllComponents(s)))
                {
                    yield return subcomponent;
                }
            }

            [Theory]
            [MemberData(nameof(GetsAffectedComponentPath_Data))]
            public void GetsAffectedComponentPath(string expectedPath, string inputPath)
            {
                var incident = new Incident()
                {
                    Source = new IncidentSourceData()
                    {
                        CreateDate = new DateTime(2018, 10, 10)
                    }
                };

                var input = new ParsedIncident(incident, inputPath, ComponentStatus.Up);

                var result = Provider.Get(input);

                Assert.Equal(expectedPath, result);
            }
        }

        public class EventAffectedComponentPathProviderTest
        {
            public EventAffectedComponentPathProvider Provider { get; }

            public EventAffectedComponentPathProviderTest()
            {
                Provider = new EventAffectedComponentPathProvider();
            }
        }
    }
}
