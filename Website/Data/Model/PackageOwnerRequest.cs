﻿using System;

namespace NuGetGallery.Data.Model
{
    public class PackageOwnerRequest : IEntity
    {
        public int PackageRegistrationKey { get; set; }
        public int NewOwnerKey { get; set; }
        public User NewOwner { get; set; }
        public User RequestingOwner { get; set; }
        public int RequestingOwnerKey { get; set; }
        public string ConfirmationCode { get; set; }
        public DateTime RequestDate { get; set; }
        public int Key { get; set; }
    }
}