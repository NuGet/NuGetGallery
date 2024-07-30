// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    public class DurationMeasurement<T>
    {
        public DurationMeasurement(T result, TimeSpan duration)
        {
            Value = result;
            Duration = duration;
        }

        public T Value { get; }
        public TimeSpan Duration { get; }
    }
}