// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// A warning for packages that are missing licensing metadata.
    /// </summary>
    public class MissingLicenseValidationMessage : IValidationMessage
    {
        private string DocumentationLink => $"<a href=\"https://aka.ms/nuget/authoring-best-practices#licensing\" aria-label=\"{Strings.UploadPackage_LearnMore_PackagingLicense}\">{Strings.UploadPackage_LearnMore}</a>.";

        private readonly string _baseMessage;

        public MissingLicenseValidationMessage(string basePlainTextMessage)
        {
            if (string.IsNullOrWhiteSpace(basePlainTextMessage))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, nameof(basePlainTextMessage)), nameof(basePlainTextMessage));
            }

            _baseMessage = basePlainTextMessage;
            PlainTextMessage = $"{_baseMessage} {Strings.UploadPackage_LearnMore}: https://aka.ms/nuget/authoring-best-practices#licensing.";
        }

        public string PlainTextMessage { get; }

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage
            => HttpUtility.HtmlEncode(_baseMessage) + " " + DocumentationLink;
    }
}