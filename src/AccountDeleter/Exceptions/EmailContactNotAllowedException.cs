// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Used to indicate that we attempted to get contact information for a user that disallowed email contact.
    /// </summary>
    public class EmailContactNotAllowedException : Exception { }
}
