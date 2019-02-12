// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Messages
{
    public class LinkMessageFormat
    {
        public string Message { get; }
        public string Link { get; }
        public bool IsUrlMessage { get; }

        /// <summary>
        /// The place holder object for holding the link formatting for the specified message
        /// </summary>
        /// <param name="message">The data to be linked</param>
        /// <param name="link">The redirection link for the data. Specify this as null if you want to show only data without link</param>
        public LinkMessageFormat(string message, string link = null)
        {
            Message = message;
            Link = link;
            IsUrlMessage = !string.IsNullOrEmpty(link);
        }
    }
}