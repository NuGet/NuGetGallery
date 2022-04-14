// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// A generic license url deprecation message that appends a "Learn more" documentation link to a given base message.
    /// </summary>
    public class LicenseUrlDeprecationValidationMessage : IValidationMessage
    {
        private string DeprecationLink => $"<a href=\"{GalleryConstants.LicenseDeprecationUrl}\" aria-label=\"{Strings.UploadPackage_LearnMore_LicenseUrlDreprecation}\">{Strings.UploadPackage_LearnMore}</a>.";

        private readonly string _baseMessage;
        
        public LicenseUrlDeprecationValidationMessage(string basePlainTextMessage)
        {
            if (string.IsNullOrWhiteSpace(basePlainTextMessage))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, nameof(basePlainTextMessage)), nameof(basePlainTextMessage));
            }

            _baseMessage = basePlainTextMessage;
            PlainTextMessage = $"{_baseMessage} {Strings.UploadPackage_LearnMore}: {GalleryConstants.LicenseDeprecationUrl}.";
        }

        public string PlainTextMessage { get; }

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage
            => HttpUtility.HtmlEncode(_baseMessage) + " " + DeprecationLink;
    }
}