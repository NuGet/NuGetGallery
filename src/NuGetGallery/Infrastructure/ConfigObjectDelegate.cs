using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Infrastructure
{
    public class ConfigObjectDelegate<A>
    {
        private Func<A> _factoryMethod;
        private A _cachedObject;

        private Func<Object> _getConfigValue;
        private Object _currentConfigValue;

        public ConfigObjectDelegate(Func<A> factoryMethod, Func<Object> getConfigValue)
        {
            _factoryMethod = factoryMethod;
            _getConfigValue = getConfigValue;
        }

        public A Get()
        {
            var oldConfigValue = _currentConfigValue;
            _currentConfigValue = _getConfigValue();

            if (_cachedObject == null || !oldConfigValue.Equals(_currentConfigValue))
            {
                _cachedObject = _factoryMethod();
            }

            return _cachedObject;
        }
    }
}