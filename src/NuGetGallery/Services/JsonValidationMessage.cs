// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Validation message representation for returning in Json responses for Gallery web pages.
    /// </summary>
    public class JsonValidationMessage
    {
        public JsonValidationMessage(string message)
        {
            PlainTextMessage = message;
            RawHtmlMessage = null;
        }

        public JsonValidationMessage(IValidationMessage message)
        {
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