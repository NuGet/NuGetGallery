// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace NuGetGallery
{
    [DebuggerDisplay("{PublisherName,nq}: {Description,nq}")]
    public sealed class TrustedPublisherViewModel
    {
        public int Key { get; set; }

        /// <summary>
        /// User provided description.
        /// </summary>
        public string PolicyName { get; set; }

        /// <summary>
        /// NuGet package owner.
        /// </summary>
        public string Owner { get; set; }

        public PublisherDetailsViewModel PublisherDetails { get; set; }

        public string PublisherName => PublisherDetails?.Name ?? string.Empty;
    }
}
