// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class UpdateListedRequest
    {
        public UpdateListedRequest()
        {
            Packages = new List<string>();
        }

        public List<string> Packages { get; set; }

        public bool Listed { get; set; }
    }
}
