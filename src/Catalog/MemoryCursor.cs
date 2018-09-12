// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class MemoryCursor : ReadWriteCursor
    {
        public static DateTime MinValue = DateTime.MinValue.ToUniversalTime();
        public static DateTime MaxValue = DateTime.MaxValue.ToUniversalTime();

        public static MemoryCursor CreateMin() { return new MemoryCursor(MinValue); }
        public static MemoryCursor CreateMax() { return new MemoryCursor(MaxValue); }

        public MemoryCursor(DateTime value)
        {
            Value = value;
        }

        public MemoryCursor()
        {
            // TODO: Complete member initialization
        }

        public override Task LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public override Task SaveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}