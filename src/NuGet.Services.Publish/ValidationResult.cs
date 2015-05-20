// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Services.Publish
{
    public class ValidationResult
    {
        public PackageIdentity PackageIdentity { get; set; }
        public IList<string> Errors { get; private set; }

        public ValidationResult()
        {
            Errors = new List<string>();
        }

        public bool HasErrors { get { return Errors.Count > 0; } }
    }
}