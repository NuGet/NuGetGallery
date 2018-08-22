// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;

namespace StatusAggregator.Table
{
    public static class TableUtility
    {
        /// <summary>
        /// The <see cref="ITableEntity.ETag"/> to provide when the existing content in the table is unimportant.
        /// E.g. "if match any".
        /// </summary>
        public const string ETagWildcard = "*";
    }
}
