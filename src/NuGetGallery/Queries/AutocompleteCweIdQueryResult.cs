// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class AutocompleteCweIdQueryResult
    {
        public AutocompleteCweIdQueryResult(string cweId, string name, string description)
        {
            CweId = cweId ?? throw new ArgumentNullException(nameof(cweId));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public string CweId { get; }
        public string Name { get; }
        public string Description { get; }
    }
}