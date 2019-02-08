// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Validation message representation for returning in Json responses for Gallery web pages.
    /// </summary>
    public class JsonValidationMessage
    {
        public JsonValidationMessage(string message)
        {
            PlainTextMessage = message ?? throw new ArgumentNullException(nameof(message));
            RawHtmlMessage = null;
        }

        public JsonValidationMessage(IGalleryMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.HasRawHtmlRepresentation)
            {
                PlainTextMessage = null;
                RawHtmlMessage = message.RawHtmlMessage;
            }
            else
            {
                PlainTextMessage = message.PlainTextMessage;
                RawHtmlMessage = null;
            }
        }

        public string PlainTextMessage { get; }
        public string RawHtmlMessage { get; }
    }
}