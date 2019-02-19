// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class AutocompleteCweIdQueryResults
    {
        public AutocompleteCweIdQueryResults(string errorMessage)
        {
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(errorMessage);
            Success = false;
        }

        public AutocompleteCweIdQueryResults(IReadOnlyCollection<AutocompleteCweIdQueryResult> results)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
            Success = true;
        }

        public bool Success { get; }

        public string ErrorMessage { get; set; }

        public IReadOnlyCollection<AutocompleteCweIdQueryResult> Results { get; }
    }
}