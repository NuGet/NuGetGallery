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
        /// Format the number to a 3 digit representation plus a letter to represent the scale (K for kilo, M for mega, or B for billion)
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static string ToKiloFormat(this int number)
        {
            var thresholds = new[]
            {
                new { Threshold = 1_000_000_000, Transform = new Func<int, float>((x) => x / 1_000_000_000f), Format = "{0:F3}B", Trim = true },
                new { Threshold = 100_000_000, Transform =  new Func<int, float>((x) => x / 1_000_000), Format = "{0:F0}M", Trim = false},
                new { Threshold = 10_000_000, Transform =  new Func<int, float>((x) => x / 1_000_000f), Format = "{0:F2}M", Trim = true},
                new { Threshold = 1_000_000, Transform =  new Func<int, float>((x) => x / 1_000_000f), Format = "{0:F3}M", Trim = true},
                new { Threshold = 100_000, Transform = new Func<int, float>((x) => x / 1_000), Format = "{0}K", Trim = false},
                new { Threshold = 10_000, Transform = new Func<int, float>((x) => x / 1_000f), Format = "{0:F2}K", Trim = true},
                new { Threshold = 1_000, Transform = new Func<int, float>((x) => x / 1_000f), Format = "{0:F3}K", Trim = true},
                new { Threshold = int.MinValue, Transform = new Func<int, float>((x) => x), Format = "{0}", Trim = false}
            };

            var elem = thresholds.First(d => number >= d.Threshold);
            var strFormat = string.Format(elem.Format, elem.Transform(number));

            return elem.Trim ? strFormat.Remove(4, 1) : strFormat;
        }
    }
}