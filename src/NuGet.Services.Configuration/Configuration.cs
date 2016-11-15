// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// Base class to extend when using an <see cref="IConfigurationFactory"/>.
    /// </summary>
    public abstract class Configuration
    {
        protected Configuration()
        {
            CreatedTime = DateTimeOffset.UtcNow;
        }

        public DateTimeOffset CreatedTime { get; }
    }
}
