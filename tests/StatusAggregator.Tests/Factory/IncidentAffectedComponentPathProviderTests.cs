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
    public class IncidentAffectedComponentPathProviderTests
    {
        public class TheGetMethod
            : IncidentAffectedComponentPathProviderTest
        {
            public static IEnumerable<object[]> GetsAffectedComponentPath_Data
            {
                get
                {
                    var root = new NuGetServiceComponentFactory().Create();
                    return GetAllComponents(root).Select(c => new object[] { c.Path });
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
            public void GetsAffectedComponentPath(string path)
            {
                var incident = new Incident()
                {
                    Source = new IncidentSourceData()
                    {
                        CreateDate = new DateTime(2018, 10, 10)
                    }
                };

                var input = new ParsedIncident(incident, path, ComponentStatus.Up);

                var result = Provider.Get(input);

                Assert.Equal(path, result);
            }
        }

        public class IncidentAffectedComponentPathProviderTest
        {
            public IncidentAffectedComponentPathProvider Provider { get; }

            public IncidentAffectedComponentPathProviderTest()
            {
                Provider = new IncidentAffectedComponentPathProvider();
            }
        }
    }
}
