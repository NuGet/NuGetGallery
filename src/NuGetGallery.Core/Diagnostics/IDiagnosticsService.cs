// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Diagnostics
{
    public interface IDiagnosticsService
    {
        /// <summary>
        /// Gets an <see cref="IDiagnosticsSource"/> by the specified name.
        /// </summary>
        /// <param name="name">The name of the source. It's recommended that you use the unqualified type name (i.e. 'UserService').</param>
        IDiagnosticsSource GetSource(string name);
    }
}
