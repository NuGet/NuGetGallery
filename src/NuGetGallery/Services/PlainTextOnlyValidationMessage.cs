// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Cautious default implementation of <see cref="IGalleryMessage"/> that does not allow
    /// user to specify raw html response.
    /// </summary>
    public class PlainTextOnlyValidationMessage : IGalleryMessage
    {
        public PlainTextOnlyValidationMessage(string validationMessage)
        {
            PlainTextMessage = validationMessage;
        }

        public string PlainTextMessage { get; }

        public bool HasRawHtmlRepresentation => false;

        public string RawHtmlMessage => null;
    }
}