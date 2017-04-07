// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;

namespace NuGet.Services.Metadata.Catalog
{
    public class StringInterner
    {
        private readonly IDictionary<string, string> _instances = new Dictionary<string, string>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public string Intern(string value)
        {
            var output = value;

            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_instances.TryGetValue(value, out output))
                {
                    return output;
                }

                _lock.EnterWriteLock();
                try
                {
                    _instances.Add(value, value);
                    return value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }
    }
}
