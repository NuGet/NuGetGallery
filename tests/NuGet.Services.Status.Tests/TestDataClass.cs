// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;

namespace NuGet.Services.Status.Tests
{
    public abstract class TestDataClass : IEnumerable<object[]>
    {
        public abstract IEnumerable<object[]> Data { get; }

        public IEnumerator<object[]> GetEnumerator() => Data.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
