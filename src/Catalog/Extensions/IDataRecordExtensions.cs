// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System.Data
{
    /// <summary>
    /// Extension methods that make working with <see cref="IDataRecord"/> more convenient.
    /// </summary>
    public static class IDataRecordExtensions
    {
        public static DateTime ReadNullableUtcDateTime(this IDataRecord dataRecord, string columnName)
        {
            if (dataRecord == null)
            {
                throw new ArgumentNullException(nameof(dataRecord));
            }

            if (columnName == null)
            {
                throw new ArgumentNullException(nameof(columnName));
            }

            return (dataRecord[columnName] == DBNull.Value ? DateTime.MinValue : ReadDateTime(dataRecord, columnName)).ForceUtc();
        }

        public static DateTime ReadDateTime(this IDataRecord dataRecord, string columnName)
        {
            if (dataRecord == null)
            {
                throw new ArgumentNullException(nameof(dataRecord));
            }

            if (columnName == null)
            {
                throw new ArgumentNullException(nameof(columnName));
            }

            return dataRecord.GetDateTime(dataRecord.GetOrdinal(columnName));
        }
    }
}