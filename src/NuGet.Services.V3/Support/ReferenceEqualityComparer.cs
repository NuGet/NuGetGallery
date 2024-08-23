// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NuGet.Services
{
    /// <summary>
    /// Source: https://stackoverflow.com/a/35520207
    /// </summary>
    public sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Default = new ReferenceEqualityComparer<T>();

        private ReferenceEqualityComparer()
        {
        }

        [DebuggerStepThrough]
        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        [DebuggerStepThrough]
        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
