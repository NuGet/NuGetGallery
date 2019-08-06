// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class AccountDeleteMessage
    {
        public AccountDeleteMessage(string username, string source)
        {
            Username = username;
            Source = source;
        }

        /// <summary>
        /// Username specifying the account this message is for.
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Source of the delete message. This will be validated against known sources.
        /// </summary>
        public string Source { get; }
    }
}
