// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class PackageShouldNotBeSignedUserFixableValidationMessage : IValidationMessage
    {
        public string PlainTextMessage => Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates;

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage 
            => Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates 
                + " " 
                + Strings.UploadPackage_PackageIsSignedButMissingCertificate_ManageCertificate;
    }
}