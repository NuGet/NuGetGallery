// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public interface IHttpContentWrapper : IDisposable
    {
        Task<string> ReadAsStringAsync();
    }
}
