// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services
{
    public class IdAndValue<T>
    {
        public IdAndValue(string id, T value)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Value = value;
        }

        public string Id { get; }
        public T Value { get; }
    }
}
