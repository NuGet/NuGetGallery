// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;

namespace NuGetGallery
{
    public static class HttpExceptionExtensions
    {
        private const int UnspecifiedError = -2147467259; // HRESULT 0x80004005

        public static bool IsMaxRequestLengthExceeded(this HttpException e)
        {
            return e.ErrorCode == UnspecifiedError
                && e.Message.Equals("Maximum request length exceeded.", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}