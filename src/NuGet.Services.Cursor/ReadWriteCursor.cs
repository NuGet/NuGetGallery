// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Cursor
{
    public abstract class ReadWriteCursor<T> : ReadCursor<T>
    {
        public abstract Task Save(CancellationToken cancellationToken);
    }
}
