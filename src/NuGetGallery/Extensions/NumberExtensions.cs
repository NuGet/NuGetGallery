// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;

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

        /// <summary>
        /// Format the number to a 1 decimal precision plus a letter to represent the scale (K for kilo, M for mega, or B for billion)
        /// </summary>
        /// <param name="number">The number to format</param>
        /// <returns>String representation of the formatted number</returns>
        public static string ToKiloFormat(this int number)
        {
            // To avoid overflow (with Math.Abs()). 1 difference won't make a difference in the simplified format :)
            if (number == int.MinValue)
            {
                number = -1 * int.MaxValue;
            }

            if (Math.Abs(number) < 1000)
            {
                return number.ToString();
            }

            var powers = new[]
            {
                new { Value = 1_000_000_000f, Unit = 'B'},
                new { Value = 1_000_000f    , Unit = 'M'},
                new { Value = 1_000f        , Unit = 'K'}
            };

            return powers
                .Where(pow => Math.Abs(Math.Round(number / pow.Value, 3)) >= 1f)
                .Select(pow => string.Format(CultureInfo.InvariantCulture, "{0:F1}{1}", number / pow.Value, pow.Unit))
                .First();
        }
    }
}