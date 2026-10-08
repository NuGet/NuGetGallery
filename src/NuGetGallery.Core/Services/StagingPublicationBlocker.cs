// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Identifies a publishing restriction and the action needed to resolve it.
    /// </summary>
    public sealed class StagingPublicationBlocker
    {
        public StagingPublicationBlocker(string code, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string Code { get; }

        public string Message { get; }
    }
}
