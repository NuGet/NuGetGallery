// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.CDNLogsSanitizer
{
    public interface ISanitizer
    {
        /// <summary>
        /// Update the a CDN log line as necessary.
        /// </summary>
        /// <param name="logLine">The log line.</param>
        void SanitizeLogLine(ref string logLine);
    }
}
