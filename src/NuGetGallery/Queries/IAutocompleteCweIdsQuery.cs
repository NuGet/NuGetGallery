// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        /// Returns a <see cref="IReadOnlyCollection{CweIdAutocompleteQueryResult}"/>.
        /// </returns>
        /// <exception cref="FormatException">Thrown when the format of the partial CWE Id is invalid or could not be determined.</exception>
        IReadOnlyCollection<AutocompleteCweIdQueryResult> Execute(string queryString);
    }
}