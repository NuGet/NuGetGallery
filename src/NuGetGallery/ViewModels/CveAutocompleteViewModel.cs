// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class CveAutocompleteViewModel
    {
        public CveAutocompleteViewModel(string errorMessage)
        {
            Success = false;
            ErrorMessage = errorMessage;
        }

        public CveAutocompleteViewModel(IReadOnlyCollection<CveIdAutocompleteQueryResult> results)
        {
            Success = true;
            Results = new List<CveIdAutocompleteQueryResult>(results);
        }

        public bool Success { get; }
        public string ErrorMessage { get; }

        public List<CveIdAutocompleteQueryResult> Results { get; }
    }
}