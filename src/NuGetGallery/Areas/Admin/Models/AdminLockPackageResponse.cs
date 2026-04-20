// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminLockPackageResponse
    {
        public List<AdminLockPackageResult> Results { get; set; }
    }

    public class AdminLockPackageResult
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Status { get; set; }
    }

    public static class AdminLockPackageStatus
    {
        public const string Accepted = "accepted";
        public const string NotFound = "notFound";
        public const string Invalid = "invalid";
    }
}
