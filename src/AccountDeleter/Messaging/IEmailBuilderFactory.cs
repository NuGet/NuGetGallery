// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Messaging.Email;

namespace NuGetGallery.AccountDeleter
{
    public interface IEmailBuilderFactory
    {
        /// <summary>
        /// Returns an email builder that is source aware
        /// </summary>
        /// <param name="source"></param>
        /// <returns>An email builder that can build a message for messages from given source.</returns>
        /// <exception cref="UnknownSourceException">Requested source was unknown/not present in configuration</exception>
        IEmailBuilder GetEmailBuilder(string source, bool success);
    }
}
