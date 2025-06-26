// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace NuGetGallery
{
    [DebuggerDisplay("{Name,nq}")]
    public abstract class PublisherDetailsViewModel
    {
        protected PublisherDetailsViewModel() { }

        /// <summary>
        /// Publisher name, e.g. GitHub.
        /// </summary>
        public abstract string Name { get; }
    }
}
