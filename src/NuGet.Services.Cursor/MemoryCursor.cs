// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Cursor
{
    public class MemoryCursor : ReadWriteCursor<DateTimeOffset>
    {
        public static DateTimeOffset MinValue = DateTimeOffset.MinValue.ToUniversalTime();
        public static DateTimeOffset MaxValue = DateTimeOffset.MaxValue.ToUniversalTime();

        public static MemoryCursor CreateMin() { return new MemoryCursor(MinValue); }
        public static MemoryCursor CreateMax() { return new MemoryCursor(MaxValue); }

        public MemoryCursor(DateTimeOffset value)
        {
            Value = value;
        }

        public MemoryCursor()
        {
            // TODO: Complete member initialization
        }

        public override Task Load(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public override Task Save(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }
}
