// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public interface IAutoCompleteCveIdsQuery
    {
        /// <summary>
        /// Returns the CVE Id's and (truncated) descriptions matching 
        /// the user-provided <paramref name="partialId"/>.
        /// </summary>
        /// <param name="partialId">The partial CVE Id provided by the user.</param>
        /// <returns>
        /// Returns a <see cref="IReadOnlyCollection{CveIdAutocompleteQueryResult}"/>.
        /// </returns>
        /// <exception cref="FormatException">Thrown when the format of the partial CVE Id is invalid or could not be determined.</exception>
        IReadOnlyCollection<CveIdAutocompleteQueryResult> Execute(string partialId);
    }
}