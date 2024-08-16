// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public interface IHttpResponseMessageWrapper : IDisposable
    {
        bool IsSuccessStatusCode { get; }

        string ReasonPhrase { get; set; }

        HttpStatusCode StatusCode { get; set; }

        IHttpContentWrapper Content { get; }
    }
}
