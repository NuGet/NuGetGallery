// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Exception to indicate that a message was processed that had a source that we don't know about. Note this could indicate a malicious actor, or out of date configuration.
    /// </summary>
    public class UnknownSourceException : Exception { }
}
