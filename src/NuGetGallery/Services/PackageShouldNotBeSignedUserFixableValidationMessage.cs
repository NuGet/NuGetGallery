// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Represents an error message to be displayed when user uploads a signed package but theirs account
    /// was not setup to accept signed packages. We want to display some additional text when the error is
    /// presented on a web page, so this class abuses the messaging a little, no actual HTML is generated.
    /// </summary>
    public class PackageShouldNotBeSignedUserFixableValidationMessage : IGalleryMessage
    {
        public string PlainTextMessage => Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates;

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage 
            => HttpUtility.HtmlEncode(
                Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates 
                + " " 
                + Strings.UploadPackage_PackageIsSignedButMissingCertificate_ManageCertificate);
    }
}