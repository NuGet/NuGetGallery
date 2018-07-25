// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Revalidate
{
    public class StartNextRevalidationOperation
    {
        /// <summary>
        /// The result of attempting to start the next revalidation.
        /// </summary>
        public RevalidationResult Result { get; set; }
    }
}
