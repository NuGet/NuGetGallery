// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public interface IAutocompleteCveIdsQuery
    {
        /// <summary>
        /// Returns the CVE Id's and (truncated) descriptions matching 
        /// the user-provided <paramref name="partialId"/>.
        /// </summary>
        /// <param name="partialId">The partial CVE Id provided by the user.</param>
        /// <returns>
        /// Returns a <see cref="AutocompleteCveIdQueryResults"/>.
        /// </returns>
        AutocompleteCveIdQueryResults Execute(string partialId);
    }
}