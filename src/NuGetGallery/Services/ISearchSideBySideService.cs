// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// A service which two search queries in parallel and return the results for the purposes of side-by-side
    /// comparison.
    /// </summary>
    public interface ISearchSideBySideService
    {
        Task<SearchSideBySideViewModel> SearchAsync(string searchTerm, User currentUser);
        Task RecordFeedbackAsync(SearchSideBySideViewModel viewModel, string searchUrl);
    }
}