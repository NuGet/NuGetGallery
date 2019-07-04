// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Exception is thrown when a user was expected to be found, but was not.
    /// </summary>
    public class UserNotFoundException : Exception
    {
    }
}
