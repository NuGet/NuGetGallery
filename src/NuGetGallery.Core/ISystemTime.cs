// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Provides a mockable way to access system time related functionality
    /// </summary>
    public interface ISystemTime
    {
        DateTimeOffset UtcNow { get; }
    }
}
