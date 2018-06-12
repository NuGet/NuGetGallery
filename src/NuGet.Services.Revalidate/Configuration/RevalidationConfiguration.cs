// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Revalidate
{
    public class RevalidationConfiguration
    {
        /// <summary>
        /// The configurations used to initialize the revalidation state.
        /// </summary>
        public InitializationConfiguration Initialization { get; set; }
    }
}
