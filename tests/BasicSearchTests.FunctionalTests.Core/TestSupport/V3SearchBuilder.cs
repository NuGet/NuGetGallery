// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace BasicSearchTests.FunctionalTests.Core.TestSupport
{
    public class V3SearchBuilder : QueryBuilder
    {
        public V3SearchBuilder() : base("/query?") { }
    }
}