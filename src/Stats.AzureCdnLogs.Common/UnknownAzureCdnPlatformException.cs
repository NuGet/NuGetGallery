// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Stats.AzureCdnLogs.Common
{
    [Serializable]
    public sealed class UnknownAzureCdnPlatformException
        : Exception
    {
        public UnknownAzureCdnPlatformException(string prefix)
            : base(string.Format(CultureInfo.InvariantCulture, "The file prefix '{0}' is not recognized as a valid Azure Premium CDN platform.", prefix))
        {
        }
    }
}