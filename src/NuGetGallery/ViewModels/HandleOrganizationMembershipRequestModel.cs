// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class HandleOrganizationMembershipRequestModel
    {
        /// <summary>
        /// If true, this is an attempt to confirm a request.
        /// If false, this is an attempt to reject a request.
        /// </summary>
        public bool Confirm { get; }
        public string OrganizationName { get; }
        public bool Successful => string.IsNullOrEmpty(FailureReason);
        public string FailureReason { get; }

        public HandleOrganizationMembershipRequestModel(bool confirm, Organization organization)
        {
            Confirm = confirm;
            OrganizationName = organization.Username;
        }

        public HandleOrganizationMembershipRequestModel(bool confirm, Organization organization, string failureReason)
            : this(confirm, organization)
        {
            FailureReason = failureReason;
        }
    }
}