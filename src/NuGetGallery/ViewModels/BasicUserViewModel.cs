// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class BasicUserViewModel
    {
        public string EmailAddress { get; set; }
        public string Username { get; set; }
        public bool IsOrganization { get; set; }
    }
}