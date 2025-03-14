// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    internal sealed class SystemToolException : Exception
    {
        public SystemToolException()
        {
        }

        public SystemToolException(string message)
            : base(message)
        {
        }

        public SystemToolException(string format, params object[] args)
            : base(string.Format(CultureInfo.CurrentCulture, format, args))
        {
        }

        public SystemToolException(string message, Exception exception)
            : base(message, exception)
        {
        }
    }
}
