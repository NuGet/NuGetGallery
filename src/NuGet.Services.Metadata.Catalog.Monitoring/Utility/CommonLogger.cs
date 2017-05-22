// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// <see cref="Common.ILogger"/> wrapper for <see cref="ILogger"/>.
    /// </summary>
    public class CommonLogger : Common.ILogger
    {
        public CommonLogger(ILogger logger)
        {
            InternalLogger = logger ?? throw new ArgumentNullException(nameof(ILogger));
        }

        public ILogger InternalLogger { get; private set; }

        public void LogDebug(string data)
        {
            InternalLogger.LogDebug(data);
        }

        public void LogVerbose(string data)
        {
            InternalLogger.LogInformation(data);
        }

        public void LogInformation(string data)
        {
            InternalLogger.LogInformation(data);
        }

        public void LogMinimal(string data)
        {
            InternalLogger.LogInformation(data);
        }

        public void LogWarning(string data)
        {
            InternalLogger.LogWarning(data);
        }

        public void LogError(string data)
        {
            InternalLogger.LogError(data);
        }

        public void LogInformationSummary(string data)
        {
            InternalLogger.LogInformation(data);
        }

        public void LogErrorSummary(string data)
        {
            InternalLogger.LogError(data);
        }
    }
}
