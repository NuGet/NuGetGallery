// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
namespace NuGetGallery.Operations
{
    public class User
    {
        public int Key { get; set; }
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public string UnconfirmedEmailAddress { get; set; }

        public IEnumerable<string> PackageRegistrationIds { get; set; }
    }
}
