// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class AutocompleteCveIdQueryResults
    {
        public AutocompleteCveIdQueryResults(string errorMessage)
        {
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(errorMessage);
            Success = false;
        }

        public AutocompleteCveIdQueryResults(IReadOnlyCollection<AutocompleteCveIdQueryResult> results)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
            Success = true;
        }

        public bool Success { get; }

        public string ErrorMessage { get; set; }

        public IReadOnlyCollection<AutocompleteCveIdQueryResult> Results { get; }
    }
}