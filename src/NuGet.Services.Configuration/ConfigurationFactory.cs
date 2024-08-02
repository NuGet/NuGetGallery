// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NuGet.Services.Configuration
{
    /// <summary>
    /// <see cref="IConfigurationFactory"/> implementation that uses an <see cref="IConfigurationProvider"/> to inject configuration.
    /// </summary>
    public class ConfigurationFactory : IConfigurationFactory
    {
        private readonly IConfigurationProvider _configProvider;

        public ConfigurationFactory(IConfigurationProvider configProvider)
        {
            if (configProvider == null)
            {
                throw new ArgumentNullException(nameof(configProvider));
            }

            _configProvider = configProvider;
        }

        public async Task<T> Get<T>() where T : Configuration, new()
        {
            var instance = new T();

            // Get the prefix specified by the ConfigurationKeyPrefixAttribute on the class if it exists.
            var classPrefix = string.Empty;
            var configNamePrefixProperty =
                (ConfigurationKeyPrefixAttribute)
                typeof(T).GetCustomAttributes(typeof(ConfigurationKeyPrefixAttribute), true).FirstOrDefault();
            if (configNamePrefixProperty != null)
            {
                classPrefix = configNamePrefixProperty.Prefix;
            }

            // Iterate over the properties and inject each with configuration.
            foreach (
                var property in
                TypeDescriptor.GetProperties(instance).Cast<PropertyDescriptor>().Where(p => !p.IsReadOnly))
            {
                await (Task) GetType()
                    .GetMethod(nameof(InjectPropertyWithConfiguration), BindingFlags.NonPublic | BindingFlags.Instance)
                    .MakeGenericMethod(typeof(T), property.PropertyType)
                    .Invoke(this, new object[] {instance, property, classPrefix});
            }

            return instance;
        }

        /// <summary>
        /// Injects a property of <param name="instance">instance</param> specified by <param name="property">a <see cref="PropertyDescriptor"/></param> with configuration.
        /// </summary>
        /// <typeparam name="T">Type of <param name="instance">the instance</param>.</typeparam>
        /// <typeparam name="TP">Type of <param name="property">the property</param>.</typeparam>
        /// <param name="instance">Instance to inject configuration into a property of.</param>
        /// <param name="property"><see cref="PropertyDescriptor"/> that describes the property to inject the configuration into.</param>
        /// <param name="classPrefix">The prefix to add to the configuration key of all properties except those that have a <see cref="ConfigurationKeyPrefixAttribute"/>.</param>
        /// <returns>A task that completes when the property has been injected into.</returns>
        private async Task InjectPropertyWithConfiguration<T, TP>(T instance, PropertyDescriptor property, string classPrefix)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            var configKey = string.IsNullOrEmpty(property.DisplayName) ? property.Name : property.DisplayName;

            // Replace the configuration key with the key specified by the ConfigurationKeyAttribute if it exists.
            var configNameProperty = property.Attributes.OfType<ConfigurationKeyAttribute>().FirstOrDefault();
            if (configNameProperty != null)
            {
                if (string.IsNullOrEmpty(configNameProperty.Key))
                {
                    throw new ArgumentException(
                        $"Configuration key for {configKey} specified by {nameof(ConfigurationKeyAttribute)} is null or empty!",
                        nameof(configNameProperty.Key));
                }

                configKey = configNameProperty.Key;
            }

            // Add the prefix specified by the ConfigurationKeyPrefixAttribute to the configuration key if it exists.
            var configNamePrefixProperty =
                property.Attributes.OfType<ConfigurationKeyPrefixAttribute>().FirstOrDefault();
            if (configNamePrefixProperty != null)
            {
                if (string.IsNullOrEmpty(configNamePrefixProperty.Prefix))
                {
                    throw new ArgumentException(
                        $"Configuration key prefix for {configKey} specified by {nameof(ConfigurationKeyPrefixAttribute)} is null or empty!",
                        nameof(configNamePrefixProperty.Prefix));
                }

                configKey = configNamePrefixProperty.Prefix + configKey;
            }
            else
            {
                configKey = classPrefix + configKey;
            }

            TP value;

            if (property.Attributes.OfType<RequiredAttribute>().Any())
            {
                // If the property is required, use GetOrThrowAsync to access configuration.
                // It will throw if the configuration is not found or invalid.
                value = await _configProvider.GetOrThrowAsync<TP>(configKey);
            }
            else
            {
                var defaultValueAttribute = property.Attributes.OfType<DefaultValueAttribute>().FirstOrDefault();
                if (defaultValueAttribute != null)
                {
                    try
                    {
                        // Use the default value specified by the DefaultValueAttribute if it can be converted into the type of the property.
                        var defaultValue =
                            (TP)
                            (defaultValueAttribute.Value.GetType() == property.PropertyType
                                ? defaultValueAttribute.Value
                                : property.Converter.ConvertFrom(defaultValueAttribute.Value));
                        value = await _configProvider.GetOrDefaultAsync(configKey, defaultValue);
                    }
                    catch (Exception)
                    {
                        throw new ArgumentException($"Default value for {configKey} specified by {nameof(DefaultValueAttribute)} is malformed ({defaultValueAttribute.Value ?? "null"})!");
                    }
                }
                else
                {
                    value = await _configProvider.GetOrDefaultAsync<TP>(configKey);
                }
            }

            property.SetValue(instance, value);
        }
    }
}
