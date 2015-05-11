// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery
{
    public class PackageOwnerConfirmationModel
    {
        public ConfirmOwnershipResult Result { get; set; }
        public string PackageId { get; set; }
        public string Username { get; set; }
    }
}