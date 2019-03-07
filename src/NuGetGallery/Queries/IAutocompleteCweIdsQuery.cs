// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IAutocompleteCweIdsQuery
    {
        /// <summary>
        /// Returns the CWE Id's and (truncated) descriptions matching 
        /// the user-provided <paramref name="queryString"/>.
        /// </summary>
        /// <param name="queryString">
        /// The partial CWE Id provided by the user, or a textual search term to lookup CWE Id's by <see cref="Cwe.Name"/>.</param>
        /// <returns>
        /// Returns an <see cref="AutocompleteCweIdQueryResults"/>.
        /// </returns>
        AutocompleteCweIdQueryResults Execute(string queryString);
    }
}