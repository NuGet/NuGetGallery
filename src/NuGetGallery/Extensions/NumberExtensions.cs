﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;

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
            var httpContext = HttpContext.Current;
            // if we fail to detect client locale from the Languages header, fall back to server locale
            
            CultureInfo clientCulture = (httpContext == null ? null : httpContext.Request.DetermineClientLocale()) ?? CultureInfo.CurrentCulture;
            return self.ToString("n0", clientCulture);
        }

    }
}