// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery
{
    public class PackageOwnerConfirmationModel
    {
        public PackageOwnerConfirmationModel(string packageId, string username, ConfirmOwnershipResult result)
        {
            Result = result;
            PackageId = packageId;
            Username = username;
        }

        public ConfirmOwnershipResult Result { get; }

        public string PackageId { get; }

        public string Username { get; }
    }
}