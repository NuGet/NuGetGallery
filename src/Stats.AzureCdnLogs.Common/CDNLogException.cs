// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.AzureCdnLogs.Common
{
    public class CDNLogException : Exception
    {
        public CDNLogException()
        {
        }

        public CDNLogException(string message)
            : base(message)
        {
        }

        public CDNLogException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
