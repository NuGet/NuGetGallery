// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery.Infrastructure
{
    public class ConfigObjectDelegate<A>
    {
        private Func<Task<A>> _factoryMethod;
        private A _cachedObject;

        private Func<Task<Object>> _getConfigValue;
        private Object _currentConfigValue;

        public ConfigObjectDelegate(Func<Task<A>> factoryMethod, Func<Task<Object>> getConfigValue)
        {
            _factoryMethod = factoryMethod;
            _getConfigValue = getConfigValue;
        }

        public async Task<A> Get()
        {
            var oldConfigValue = _currentConfigValue;
            _currentConfigValue = await _getConfigValue();

            if (_cachedObject == null || !oldConfigValue.Equals(_currentConfigValue))
            {
                _cachedObject = await _factoryMethod();
            }

            return _cachedObject;
        }
    }
}