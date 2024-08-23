// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Stats.CollectAzureCdnLogs
{
    internal interface IRawLogClient
    {
        Task<IEnumerable<Uri>> GetRawLogFileUris(Uri uri);
        Task<Stream> OpenReadAsync(Uri uri);
        Task<bool> RenameAsync(Uri uri, string newFileName);
        Task DeleteAsync(Uri uri);
    }
}