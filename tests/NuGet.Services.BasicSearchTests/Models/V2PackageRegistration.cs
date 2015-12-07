// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.BasicSearchTests.Models
{
    public class V2PackageRegistration
    {
        public string Id { get; set; }

        public int DownloadCount { get; set; }

        public IEnumerable<string> Owners { get; set; }
    }
}