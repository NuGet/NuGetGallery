// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services
{
    public static class Guard
    {
        /// <remarks>
        /// We could use <see cref="System.Diagnostics.Debug.Assert(bool, string)"/> here, but it's preferable in this
        /// case to even fail on a non-Debug build. The goal of this method is to allow the implementor to embed more
        /// intent into the implementation so that future code changes are less likely to introduce bugs and so that
        /// the implementation makes more sense to future readers. This is, of course, in addition to unit test
        /// coverage.
        /// </remarks>
        public static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <remarks>
        /// Similar to <see cref="System.Diagnostics.Debug.Fail(string)"/> but run in Release builds.
        /// </remarks>
        public static void Fail(string message)
        {
            Assert(false, message);
        }
    }
}
