// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Represents a message that may include a raw HTML message (if we want to present some links in the message, for example).
    /// </summary>
    /// <remarks>
    /// Implementations should sanitize all input used to generate <see cref="IGalleryMessage.RawHtmlMessage"/> value.
    /// We should never have a generic implementation that would accept a raw HTML message as a constructor argument and
    /// simply returns it as a value of <see cref="IGalleryMessage.RawHtmlMessage"/>.
    /// </remarks>
    public interface IGalleryMessage
    {
        /// <summary>
        /// Plain text representation of the validation message. If pasted into HTML, will be html encoded.
        /// </summary>
        string PlainTextMessage { get; }

        /// <summary>
        /// An indicator that raw HTML representation is available.
        /// </summary>
        bool HasRawHtmlRepresentation { get; }

        /// <summary>
        /// HANDLE WITH EXTREME CARE. Raw HTML representation of the message.
        /// Under no conditions may it contain unvalidated user data.
        /// </summary>
        string RawHtmlMessage { get; }
    }
}
