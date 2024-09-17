// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    public static class ConfigurationUtility
    {
        /// <summary>
        /// Converts a string into T.
        /// </summary>
        /// <typeparam name="T">Type that value will be converted into.</typeparam>
        /// <param name="value">String to convert.</param>
        /// <returns>Value converted into T.</returns>
        /// <exception cref="NotSupportedException">Thrown when a conversion from string to T is impossible.</exception>
        public static T ConvertFromString<T>(string value)
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter != null)
            {
                return (T)converter.ConvertFromString(value);
            }

            throw new NotSupportedException("No converter exists from string to " + typeof(T).Name + "!");
        }

        /// <summary>
        /// Injects secret into a string trying to use cached value first. If the value is absent
        /// in cache, falls back to actually querying underlying secret store.
        /// </summary>
        /// <param name="value">String to inject secret into.</param>
        /// <param name="secretInjector">Caching secret injector to use.</param>
        /// <param name="logger">Logger.</param>
        /// <returns>String with secrets injected.</returns>
        public static string InjectCachedSecret(string value, ICachingSecretInjector secretInjector, ILogger logger)
        {
            if (secretInjector.TryInjectCached(value, logger, out var injectedValue))
            {
                return injectedValue;
            }
            return secretInjector.Inject(value, logger);
        }

        public static IServiceCollection ConfigureInjected<T>(this IServiceCollection services, string sectionPrefix)
            where T : class
            => services.AddSingleton(sp => GetInjectedOptions<T>(sp, sectionPrefix));

        private static IConfigureOptions<T> GetInjectedOptions<T>(IServiceProvider sp, string sectionPrefix)
            where T : class
        {
            return new ConfigureNamedOptions<T, IConfiguration>(
                Options.DefaultName,
                sp.GetRequiredService<IConfiguration>(),
                (settings, configuration) =>
                    new SecretInjectedConfiguration(
                        configuration.GetSection(sectionPrefix),
                        sp.GetRequiredService<ICachingSecretInjector>(),
                        sp.GetRequiredService<ILogger<SecretInjectedConfiguration>>())
                    .Bind(settings));
        }
    }
}
