// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery.Configuration.Factory
{
    /// <summary>
    /// A factory that distributes a cached instance of T and recreates it when configuration values have changed.
    /// </summary>
    /// <typeparam name="T">The type of the instance to cache.</typeparam>
    public class ConfigObjectDelegate<T>
    {
        private Func<object[], T> _factoryMethod;
        private T _cachedObject;

        private string[] _configNames;
        private object[] _appConfigValues;

        private Task<T> _currentGetTask;

        private object _lockObject = new object();

        /// <summary>
        /// Constructs a factory from the given configuration service, factory method, and configuration names.
        /// 
        /// Note that the array of configNames is aligned with the array of objects passed to the factory method.
        /// In other words, the value returned from the configuration service for configNames[0] is passed to the factory method as objects[0].
        /// </summary>
        /// <param name="configService">The configuration service to fetch configuration from.</param>
        /// <param name="factoryMethod">A method that constructs an instance of T from the values returned from the configuration service.</param>
        /// <param name="configNames">Names of configuration to fetch from the configuration service.</param>
        public ConfigObjectDelegate(Func<object[], T> factoryMethod, string[] configNames)
        {
            _factoryMethod = factoryMethod;
            _configNames = configNames;
            _appConfigValues = new object[_configNames.Length];
        }

        public ConfigObjectDelegate(Func<object[], T> factoryMethod, string configName)
            : this(factoryMethod, new string[] { configName })
        {
        }

        /// <summary>
        /// Synchronously returns the cached object.
        /// Starts an asynchronous task to refresh the cached object.
        /// </summary>
        /// <returns>The cached object.</returns>
        public T Get(IGalleryConfigurationService configService)
        {
            // Queue a task to fetch and refresh the cached object
            var getAsyncTask = GetAsync(configService);
            
            if (_cachedObject == null)
            {
                // If we have not yet cached an object, we must wait on the task we just started.
                return getAsyncTask.Result;
            }

            return _cachedObject;
        }

        /// <summary>
        /// Asynchronously returns and refreshes the cached object.
        /// If the cached object is already in the process of being fetched and refreshed, it returns the result of that task.
        /// </summary>
        /// <returns>The cached object.</returns>
        public Task<T> GetAsync(IGalleryConfigurationService configService)
        {
            // Prevent simultaneous accesses of _currentGetTask.
            lock (_lockObject)
            {
                // If no task is in progress, start one.
                if (_currentGetTask == null)
                {
                    _currentGetTask = Task.Run(() => GetAsyncInternal(configService));
                }

                var currentGetTask = _currentGetTask;

                // If the task is completed, mark it as null so that the next call will launch another task.
                if (currentGetTask.IsCompleted)
                {
                    _currentGetTask = null;
                }

                return currentGetTask;
            }
        }

        /// <summary>
        /// Asynchronously returns and refreshes the cached object, but does not check if the object is in the process of being fetched and refreshed.
        /// </summary>
        /// <returns>The cached object.</returns>
        private async Task<T> GetAsyncInternal(IGalleryConfigurationService configService)
        {
            // If we have not yet cached an object, we must create the object and cache it.
            bool mustCreateAndCacheObject = _cachedObject == null;

            // Iterate through each config value and cache the values. We much recreate the object if the values have changed.
            for (int i = 0; i < _configNames.Length; i++)
            {
                var oldConfigValue = _appConfigValues[i];

                var appConfig = await configService.GetCurrent();
                _appConfigValues[i] = appConfig.GetType().GetProperty(_configNames[i]).GetValue(appConfig);
                
                // No need to compare if we already know that we must regenerate.
                if (!mustCreateAndCacheObject && !oldConfigValue.Equals(_appConfigValues[i]))
                {
                    mustCreateAndCacheObject = true;
                }
            }

            if (mustCreateAndCacheObject)
            {
                try
                {
                    _cachedObject = _factoryMethod(_appConfigValues);
                }
                catch (Exception e)
                {
                    QuietLog.LogHandledException(e);
                }
            }

            return _cachedObject;
        }
    }
}