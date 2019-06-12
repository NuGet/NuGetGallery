// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.AccountDeleter
{
    public class AccountDeleteMessage
    {
        public AccountDeleteMessage(string subject, string source)
        {
            Subject = subject;
            Source = source;
        }

        public string Subject { get; }

        public string Source { get; }
    }
}
