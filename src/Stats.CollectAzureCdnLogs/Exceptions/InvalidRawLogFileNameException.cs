// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Stats.CollectAzureCdnLogs
{
    [Serializable]
    public sealed class InvalidRawLogFileNameException
        : Exception
    {
        public InvalidRawLogFileNameException(string fileName)
            : base(string.Format(CultureInfo.InvariantCulture, "The file '{0}' is not recognized to comply with the Azure Premium CDN raw log file naming conventions.", fileName))
        {
        }
    }
}