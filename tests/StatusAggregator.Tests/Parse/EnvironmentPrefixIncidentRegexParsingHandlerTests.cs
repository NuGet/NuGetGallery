// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using StatusAggregator.Parse;
using Xunit;

namespace StatusAggregator.Tests.Parse
{
    public abstract class EnvironmentPrefixIncidentRegexParsingHandlerTests
    {
        public abstract class TheConstructor<THandler>
        where THandler : EnvironmentPrefixIncidentRegexParsingHandler
        {
            [Fact]
            public void ThrowsWithoutEnvironmentRegexFilter()
            {
                var filters = Enumerable.Empty<IIncidentRegexParsingFilter>();
                Assert.Throws<ArgumentException>(() => Construct(filters));
            }

            [Fact]
            public void DoesNotThrowWithEnvironmentFilter()
            {
                var handler = Construct(new[] { IncidentParsingHandlerTestUtility.CreateEnvironmentFilter() });
                Assert.NotNull(handler);
            }

            protected abstract THandler Construct(IEnumerable<IIncidentRegexParsingFilter> filters);
        }
    }
}
