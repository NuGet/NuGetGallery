// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Resolves instances of <see cref="ISearchService"/> for OData scenarios
    /// that may target the preview search depending on configurations.
    /// </summary>
    public interface IHijackSearchServiceFactory
    {
        /// <summary>
        /// Create the search service to hijack OData requests.
        /// </summary>
        /// <returns>A search service to hijack OData requests.</returns>
        ISearchService GetService();
    }
}