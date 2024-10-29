// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Thrown when a valid user information is given to us, but the user information refers to a defunct account
    /// </summary>
    public class CurrentUserDeletedException : HttpException
    {
        public CurrentUserDeletedException(): base() { }
    }
}