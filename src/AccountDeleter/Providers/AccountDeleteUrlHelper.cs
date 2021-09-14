// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteUrlHelper : IUrlHelper
    {
        public string ConfirmPendingOwnershipRequest(string id, string username, string confirmationCode, bool relativeUrl)
        {
            throw new NotImplementedException();
        }

        public string ManagePackageOwnership(string id, bool relativeUrl)
        {
            throw new NotImplementedException();
        }

        public string Package(string id, string version, bool relativeUrl)
        {
            throw new NotImplementedException();
        }

        public string RejectPendingOwnershipRequest(string id, string username, string confirmationCode, bool relativeUrl)
        {
            throw new NotImplementedException();
        }
    }
}
