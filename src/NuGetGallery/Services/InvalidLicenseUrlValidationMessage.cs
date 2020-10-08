// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// The error message to be used when package contains license expression, but
    /// its license URL points to a wrong location
    /// </summary>
    public class InvalidLicenseUrlValidationMessage : IValidationMessage
    {
        private string DetailsLink => $"<a href=\"https://aka.ms/invalidNuGetLicenseUrl\" aria-label= \"{Strings.UploadPackage_LearMore_PackagingLicense}\">{Strings.UploadPackage_LearnMore}</a>.";
        private string DetailsPlainText => "https://aka.ms/invalidNuGetLicenseUrl";

        private readonly string _baseMessage;

        public InvalidLicenseUrlValidationMessage(string baseMessage)
        {
            _baseMessage = baseMessage ?? throw new ArgumentNullException(nameof(baseMessage));
        }

        public string PlainTextMessage => _baseMessage + " " + DetailsPlainText;

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage => _baseMessage + " " + DetailsLink;

    }
}