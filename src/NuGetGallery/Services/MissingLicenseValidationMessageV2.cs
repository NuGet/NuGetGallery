// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGetGallery
{
    /// <summary>
    /// A warning for packages that are missing licensing metadata.
    /// </summary>
    public class MissingLicenseValidationMessageV2 : IValidationMessage
    {
        private string DocumentationLink => $"<a href=\"https://aka.ms/nuget/authoring-best-practices#licensing\" aria-label=\"{Strings.UploadPackage_LearnMore_PackagingLicenseV2}\">{Strings.UploadPackage_LearnMore_PackagingLicenseV2}</a>.";

        private readonly string _baseMessage;

        public MissingLicenseValidationMessageV2(string basePlainTextMessage)
        {
            if (string.IsNullOrWhiteSpace(basePlainTextMessage))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, nameof(basePlainTextMessage)), nameof(basePlainTextMessage));
            }

            _baseMessage = basePlainTextMessage;
            PlainTextMessage = $"{_baseMessage} {Strings.UploadPackage_LearnMore_PackagingLicenseV2}: https://aka.ms/nuget/authoring-best-practices#licensing.";
        }

        public string PlainTextMessage { get; }

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage
            => Strings.UploadPackage_LicenseShouldBeSpecifiedHtmlV2 + " " + DocumentationLink;
    }
}