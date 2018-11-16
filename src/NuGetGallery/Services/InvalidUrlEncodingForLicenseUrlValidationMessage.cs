// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// The error message to be used when we detect an invalid URL encoding used for a license URL.
    /// Specifically, if we see that spaces are encoded as pluses instead of %20
    /// </summary>
    public class InvalidUrlEncodingForLicenseUrlValidationMessage : IValidationMessage
    {
        private string DetailsLink => $"<a href=\"https://aka.ms/malformedNuGetLicenseUrl\">{Strings.UploadPackage_LearnMore}</a>.";
        private string DetailsPlainText => "https://aka.ms/malformedNuGetLicenseUrl";

        private string BaseMessage => Strings.UploadPackage_MalformedLicenseUrl;

        public string PlainTextMessage => BaseMessage + " " + DetailsPlainText;

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage => BaseMessage + " " + DetailsLink;
    }
}