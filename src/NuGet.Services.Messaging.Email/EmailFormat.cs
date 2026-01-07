// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Messaging.Email
{
    /// <summary>
    /// Defines the formatting to be used by <see cref="EmailBuilder"/> for creating email messages.
    /// </summary>
    public enum EmailFormat
    {
        /// <summary>
        /// Indicates that <see cref="EmailBuilder"/> will create plain-text email messages, without markup. 
        /// Used as a fallback by email clients that don't support other formats.
        /// </summary>
        PlainText,

        /// <summary>
        /// Indicates that <see cref="EmailBuilder"/> will create email messages using Markdown formatting,
        /// which will be rendered as HTML by email clients.
        /// </summary>
        Markdown,

        /// <summary>
        /// Indicates that <see cref="EmailBuilder"/> will create email messages using HTML formatting.
        /// </summary>
        Html
    }
}