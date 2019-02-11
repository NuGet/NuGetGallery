// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class CveAutocompleteDataViewModel
    {
        public CveAutocompleteDataViewModel()
        {
            Items = new List<CveIdAutocompleteQueryResult>();
        }

        public List<CveIdAutocompleteQueryResult> Items { get; }
    }
}