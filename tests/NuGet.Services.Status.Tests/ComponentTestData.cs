// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Status.Tests
{
    public static class ComponentTestData
    {
        public static IEnumerable<ComponentStatus> AllComponentStatuses = Enum.GetValues(typeof(ComponentStatus)).Cast<ComponentStatus>();

        public class ComponentStatuses : TestDataClass
        {
            public override IEnumerable<object[]> Data => AllComponentStatuses.Select(s => new object[] { s });
        }

        public class ComponentStatusPairs : TestDataClass
        {
            public override IEnumerable<object[]> Data => 
                AllComponentStatuses.SelectMany(s1 => 
                    AllComponentStatuses.Select(s2 => new object[] { s1, s2 }));
        }

        public class ComponentStatusTriplets : TestDataClass
        {
            public override IEnumerable<object[]> Data =>
                AllComponentStatuses.SelectMany(s1 =>
                    AllComponentStatuses.SelectMany(s2 => 
                        AllComponentStatuses.Select(s3 => new object[] { s1, s2, s3 })));
        }
    }
}
