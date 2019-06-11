// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System.Data.Common
{
    public static class DbDataReaderExtensions
    {
        public static int? ReadInt32OrNull(this DbDataReader dataReader, string columnName)
        {
            return ReadColumnOrNull(dataReader, columnName, (r, o) => r.GetInt32(o), (int?)null);
        }

        public static string ReadStringOrNull(this DbDataReader dataReader, string columnName)
        {
            return ReadColumnOrNull(dataReader, columnName, (r, o) => r.GetString(o), nullValue: null);
        }

        private static T ReadColumnOrNull<T>(DbDataReader dataReader, string columnName, Func<DbDataReader, int, T> provideValue, T nullValue)
        {
            if (dataReader == null)
            {
                throw new ArgumentNullException(nameof(dataReader));
            }

            if (columnName == null)
            {
                throw new ArgumentNullException(nameof(columnName));
            }

            if (provideValue == null)
            {
                throw new ArgumentNullException(nameof(provideValue));
            }

            try
            {
                var ordinal = dataReader.GetOrdinal(columnName);

                if (!dataReader.IsDBNull(ordinal))
                {
                    return provideValue(dataReader, ordinal);
                }

                return nullValue;

            }
            catch (IndexOutOfRangeException)
            {
                // Thrown by DbDataReader.GetOrdinal(string) when the column does not exist.
                // The exception can be swallowed as the intention of this method is to return null instead.
                return nullValue;
            }
        }
    }
}