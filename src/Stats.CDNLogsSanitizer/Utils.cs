// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.CDNLogsSanitizer
{
    public static class Utils
    {
        /// <summary>
        /// From an array of strings returns the first index with the given value.
        /// </summary>
        /// <param name="data">The array.</param>
        /// <param name="value">The value.</param>
        /// <param name="comparer">The comparer.</param>
        /// <returns>The first index that contain the value or null if the value is not found.</returns>
        public static int? GetFirstIndex(this string[] data, string value, StringComparison comparer)
        {
            for(int i = 0; i< data.Length; i++)
            {
                if(string.Equals(data[i], value, comparer))
                {
                    return i;
                }
            }
            return null;
        }

        /// <summary>
        /// From an array of strings returns the first index with the given value. It performs a case-sensitive and culture-insensitive comparison. 
        /// </summary>
        /// <param name="data">The array.</param>
        /// <param name="value">The value.</param>
        /// <returns>The first index that contain the value or null if the value is not found.</returns>
        public static int? GetFirstIndex(this string[] data, string value)
        {
            return data.GetFirstIndex(value, StringComparison.Ordinal);
        }
    }
}
