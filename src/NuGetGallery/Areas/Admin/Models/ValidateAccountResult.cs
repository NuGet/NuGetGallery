// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.Models
{
    public class ValidateAccountResult
    {
        public ValidateAccount Account { get; set; }
        public List<ValidateAccount> Administrators { get; set; }
    }

    public class ValidateAccount
    {
        public string Username { get; set; }
        public string EmailAddress { get; set; }
    }
}