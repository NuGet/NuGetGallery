// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public static class ILoggerExtensions
    {
        /// <summary>
        /// Wraps <paramref name="logger"/> as a <see cref="Common.ILogger"/>.
        /// </summary>
        public static Common.ILogger AsCommon(this ILogger logger)
        {
            return new CommonLogger(logger);
        }
    }
}
