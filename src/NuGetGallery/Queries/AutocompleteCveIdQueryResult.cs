// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class AutocompleteCveIdQueryResult
    {
        public AutocompleteCveIdQueryResult(string cveId, string description)
        {
            CveId = cveId ?? throw new ArgumentNullException(nameof(cveId));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public string CveId { get; }
        public string Description { get; }
    }
}