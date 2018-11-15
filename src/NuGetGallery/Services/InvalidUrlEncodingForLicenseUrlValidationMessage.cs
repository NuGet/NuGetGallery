// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// The error message to be used when we detect an invalid URL encoding used for a license URL.
    /// Specifically, if we see that spaces are encoded as pluses instead of %20
    /// </summary>
    public class InvalidUrlEncodingForLicenseUrlValidationMessage : IValidationMessage
    {
        private string DetailsLink => $"<a href=\"https://docs.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu5036\">{Strings.UploadPackage_LearnMore}</a>.";
        private string DetailsPlainText => "(NU5026; https://aka.ms/malformedNuGetLicenseUrl)";

        private string BaseMessage => Strings.UploadPackage_MalformedLicenseUrl;

        public string PlainTextMessage => BaseMessage + " " + DetailsPlainText;

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage => PlainTextMessage + " " + DetailsLink;
    }
}