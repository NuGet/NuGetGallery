using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

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