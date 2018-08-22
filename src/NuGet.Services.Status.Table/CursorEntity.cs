// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace NuGet.Services.Status.Table
{
    public class CursorEntity : TableEntity
    {
        public const string DefaultPartitionKey = "cursors";

        public CursorEntity()
        {
        }

        public CursorEntity(string name, DateTime value)
            : base(DefaultPartitionKey, GetRowKey(name))
        {
            Value = value;
        }

        public string Name => RowKey;

        public DateTime Value { get; set; }

        public static string GetRowKey(string name)
        {
            return name;
        }
    }
}
