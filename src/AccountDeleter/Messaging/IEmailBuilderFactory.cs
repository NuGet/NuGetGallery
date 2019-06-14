// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Messaging.Email;

namespace NuGetGallery.AccountDeleter
{
    public interface IEmailBuilderFactory
    {
        IEmailBuilder GetEmailBuilder(string source);
    }
}
