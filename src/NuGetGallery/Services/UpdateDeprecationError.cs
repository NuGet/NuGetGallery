// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;

namespace NuGetGallery
{
    public class UpdateDeprecationError
    {
        public UpdateDeprecationError(
            HttpStatusCode status, string errorMessage)
        {
            Status = status;
            Message = errorMessage;
        }

        public HttpStatusCode Status { get; }
        public string Message { get; }
    }
}