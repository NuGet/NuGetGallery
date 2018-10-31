// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class UserAccountViewModel : AccountViewModel<User>
    {
        public ChangePasswordViewModel ChangePassword { get; set; }

        public IDictionary<CredentialKind, List<CredentialViewModel>> CredentialGroups { get; set; }

        public int SignInCredentialCount { get; set; }
    }
}
