// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Services.Publish
{
    public class EditValidationResult : ValidationResult
    {
        public bool Listed { get; set; }
        public JObject EditMetadata { get; set; }
        public JObject CatalogEntry { get; set; }
    }
}