// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGetGallery
{
    /// <summary>
    /// Extensions for int, long etc
    /// </summary>
    public static class NumberExtensions
    {
        /// <summary>
        /// Format the number by client culture
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        /// <remarks>Analogous name and signature as <see cref="DateTimeExtensions"/></remarks>
        public static string ToNuGetNumberString(this int self)
        {
            return ToNuGetNumberString((long)self);
        }

        /// <summary>
        /// Format the number by client culture
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        /// <remarks>Analogous name and signature as <see cref="DateTimeExtensions"/></remarks>
        public static string ToNuGetNumberString(this long self)
        {
            return self.ToString("n0", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Format the number of bytes into a user-friendly display label.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ToUserFriendlyBytesLabel(this long bytes)
        {
            if (bytes < 0)
            {
                throw new ArgumentOutOfRangeException("Negative values are not supported.", nameof(bytes));
            }

            if (bytes == 1)
            {
                return "1 byte";
            }

            const int scale = 1024;
            string[] orders = { "GB", "MB", "KB", "bytes" };
            var max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (var order in orders)
            {
                if (bytes >= max)
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0:##.##} {1}", decimal.Divide(bytes, max), order);
                }

                max /= scale;
            }

            return "0 bytes";
        }
    }
}