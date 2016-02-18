// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Search.Client.Correlation
{
    /// <summary>
    /// Indicated that the implementing class supports correlation id propogation, and thus excepts CorrelationIdProvider.
    /// </summary>
    public interface ICorrelated
    {
        CorrelationIdProvider CorrelationIdProvider { get; set; }
    }
}