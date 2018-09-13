// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class ReadCursor
    {
        public virtual DateTime Value { get; set; }

        public abstract Task LoadAsync(CancellationToken cancellationToken);

        public override string ToString()
        {
            return Value.ToString("O");
        }
    }
}