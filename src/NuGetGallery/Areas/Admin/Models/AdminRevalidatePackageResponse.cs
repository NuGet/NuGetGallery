// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminRevalidatePackageResponse
    {
        public List<AdminRevalidatePackageResult> Results { get; set; }
    }

    public class AdminRevalidatePackageResult
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string ValidatingType { get; set; }

        public string Status { get; set; }
    }

    public static class AdminRevalidatePackageStatus
    {
        public const string Accepted = nameof(Accepted);
        public const string Failed = nameof(Failed);
        public const string NotFound = nameof(NotFound);
    }
}
