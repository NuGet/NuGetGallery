// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchStatusResponse
    {
        /// <summary>
        /// This success boolean indicates whether all dependencies of the search service can be communicated with.
        /// Any of the properties on this type, aside from <see cref="Success"/> or <see cref="Duration"/>, can be null
        /// if <see cref="Success"/> is false. If <see cref="Success"/> is true, all properties will be non-null.
        /// </summary>
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public ServerStatus Server { get; set; }
        public IndexStatus SearchIndex { get; set; }
        public IndexStatus HijackIndex { get; set; }
        public AuxiliaryFilesMetadata AuxiliaryFiles { get; set; }
    }
}
