// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminLockUserResponse
    {
        public List<AdminLockUserResult> Results { get; set; }
    }

    public class AdminLockUserResult
    {
        public string Username { get; set; }

        public string Status { get; set; }
    }

    public static class AdminLockUserStatus
    {
        public const string Accepted = "accepted";
        public const string NotFound = "notFound";
        public const string Invalid = "invalid";
    }
}
