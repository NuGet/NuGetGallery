// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Entities
{
    public class PackageOwnerRequest
        : IEntity
    {
        public int PackageRegistrationKey { get; set; }
        public virtual PackageRegistration PackageRegistration { get; set; }
        public int NewOwnerKey { get; set; }
        public virtual User NewOwner { get; set; }
        public virtual User RequestingOwner { get; set; }
        public int RequestingOwnerKey { get; set; }
        public string ConfirmationCode { get; set; }
        public DateTime RequestDate { get; set; }
        public int Key { get; set; }
    }
}