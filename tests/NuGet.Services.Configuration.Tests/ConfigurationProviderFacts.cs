// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class ConfigurationProviderFacts
    {
        public static IEnumerable<object[]> ConfigurationTypes => new[]
        {
            new object[] {typeof(string)},
            new object[] {typeof(int)},
            new object[] {typeof(bool)},
            new object[] {typeof(DateTime)}
        };

        [Fact]
        public async Task HandlesConfigurationChanges()
        {
            // Arrange
            const string secretName = "hello i'm a secret";
            const string firstSecret = "secret1";
            const string secondSecret = "secret2";
            
            var arguments = new Dictionary<string, string>
            {
                {secretName, firstSecret}
            };

            ConfigurationProvider configProvider = new DictionaryConfigurationProvider(arguments);

            // Act
            var value1 = await configProvider.GetOrThrowAsync<string>(secretName);
            var value2 = await configProvider.GetOrDefaultAsync<string>(secretName);

            // Assert
            Assert.Equal(firstSecret, value1);
            Assert.Equal(value1, value2);

            // Arrange 2
            arguments[secretName] = secondSecret;

            // Act 2
            value1 = await configProvider.GetOrThrowAsync<string>(secretName);
            value2 = await configProvider.GetOrDefaultAsync<string>(secretName);

            // Assert 2
            Assert.Equal(secondSecret, value1);
            Assert.Equal(value1, value2);
        }

        [Theory]
        [MemberData(nameof(ConfigurationTypes))]
        public async Task HandlesNullKey(Type type)
        {
            // Arrange
            var defaultOfType = GetDefault(type);
            var memberOfType = _typeToObject[type];

            string nullKey = null;
            object[] nullKeyThrowArgs = { nullKey };
            object[] nullKeyDefaultArgs = { nullKey, null };
            object[] nullKeyDefaultSpecifiedArgs = { nullKey, memberOfType };

            ConfigurationProvider configProvider = new DummyConfigurationProvider();

            var getOrThrowMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrThrowAsync)).MakeGenericMethod(type);
            var getOrDefaultMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrDefaultAsync)).MakeGenericMethod(type);

            //// Assert

            // GetOrThrow
            await Assert.ThrowsAsync<ArgumentException>(() => (Task)getOrThrowMethod.Invoke(configProvider, nullKeyThrowArgs));
            // GetOrDefault
            Assert.Equal(defaultOfType, await (dynamic)getOrDefaultMethod.Invoke(configProvider, nullKeyDefaultArgs));
            // GetOrDefault with default specified
            Assert.Equal(memberOfType, await (dynamic)getOrDefaultMethod.Invoke(configProvider, nullKeyDefaultSpecifiedArgs));
        }

        [Theory]
        [MemberData(nameof(ConfigurationTypes))]
        public async Task HandlesEmptyKey(Type type)
        {
            // Arrange
            var defaultOfType = GetDefault(type);
            var memberOfType = _typeToObject[type];

            var emptyKey = "";
            object[] emptyKeyThrowArgs = { emptyKey };
            object[] emptyKeyDefaultArgs = { emptyKey, null };
            object[] emptyKeyDefaultSpecifiedArgs = { emptyKey, memberOfType };

            ConfigurationProvider configProvider = new DummyConfigurationProvider();

            var getOrThrowMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrThrowAsync)).MakeGenericMethod(type);
            var getOrDefaultMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrDefaultAsync)).MakeGenericMethod(type);

            //// Assert

            // GetOrThrow
            await Assert.ThrowsAsync<ArgumentException>(() => (Task)getOrThrowMethod.Invoke(configProvider, emptyKeyThrowArgs));
            // GetOrDefault
            Assert.Equal(defaultOfType, await (dynamic)getOrDefaultMethod.Invoke(configProvider, emptyKeyDefaultArgs));
            // GetOrDefault with default specified
            Assert.Equal(memberOfType, await (dynamic)getOrDefaultMethod.Invoke(configProvider, emptyKeyDefaultSpecifiedArgs));
        }

        [Theory]
        [MemberData(nameof(ConfigurationTypes))]
        public async Task HandlesKeyNotFound(Type type)
        {
            // Arrange
            var dummy = CreateDummyConfigProvider();

            var getOrThrowMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrThrowAsync)).MakeGenericMethod(type);
            var getOrDefaultMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrDefaultAsync)).MakeGenericMethod(type);

            var defaultOfType = GetDefault(type);
            var memberOfType = _typeToObject[type];

            var notFoundKey = "this key is not found";
            object[] notFoundKeyThrowArgs = { notFoundKey };
            object[] notFoundKeyDefaultArgs = { notFoundKey, null };
            object[] notFoundKeyDefaultSpecifiedArgs = { notFoundKey, memberOfType };

            //// Assert

            // GetOrThrow
            await Assert.ThrowsAsync<KeyNotFoundException>(() => (Task)getOrThrowMethod.Invoke(dummy, notFoundKeyThrowArgs));
            // GetOrDefault
            Assert.Equal(defaultOfType, await (dynamic)getOrDefaultMethod.Invoke(dummy, notFoundKeyDefaultArgs));
            // GetOrDefault with default specified
            Assert.Equal(memberOfType, await (dynamic)getOrDefaultMethod.Invoke(dummy, notFoundKeyDefaultSpecifiedArgs));
        }

        [Theory]
        [MemberData(nameof(ConfigurationTypes))]
        public async Task HandlesNullArgument(Type type)
        {
            // Arrange
            var defaultOfType = GetDefault(type);
            var memberOfType = _typeToObject[type];

            var nullValueKey = "this key has a null value";
            object[] nullValueKeyThrowArgs = { nullValueKey };
            object[] nullValueKeyDefaultArgs = { nullValueKey, null };
            object[] nullValueKeyDefaultSpecifiedArgs = { nullValueKey, memberOfType };
            
            var arguments = new Dictionary<string, string>
            {
                {nullValueKey, null}
            };

            ConfigurationProvider configProvider = new DictionaryConfigurationProvider(arguments);

            var getOrThrowMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrThrowAsync)).MakeGenericMethod(type);
            var getOrDefaultMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrDefaultAsync)).MakeGenericMethod(type);

            //// Assert
            
            // GetOrThrow
            await Assert.ThrowsAsync<ConfigurationNullOrEmptyException>(() => (Task)getOrThrowMethod.Invoke(configProvider, nullValueKeyThrowArgs));
            // GetOrDefault
            Assert.Equal(defaultOfType, await (dynamic)getOrDefaultMethod.Invoke(configProvider, nullValueKeyDefaultArgs));
            // GetOrDefault with default specified
            Assert.Equal(memberOfType, await (dynamic)getOrDefaultMethod.Invoke(configProvider, nullValueKeyDefaultSpecifiedArgs));
        }

        [Theory]
        [MemberData(nameof(ConfigurationTypes))]
        public async Task HandlesEmptyArgument(Type type)
        {
            // Arrange
            var defaultOfType = GetDefault(type);
            var memberOfType = _typeToObject[type];
            
            var emptyValueKey = "this key has an empty value";
            object[] emptyValueKeyThrowArgs = { emptyValueKey };
            object[] emptyValueKeyDefaultArgs = { emptyValueKey, null };
            object[] emptyValueKeyDefaultSpecifiedArgs = { emptyValueKey, memberOfType };

            var arguments = new Dictionary<string, string>
            {
                {emptyValueKey, "" }
            };

            ConfigurationProvider configProvider = new DictionaryConfigurationProvider(arguments);

            var getOrThrowMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrThrowAsync)).MakeGenericMethod(type);
            var getOrDefaultMethod = typeof(IConfigurationProvider).GetMethod(nameof(IConfigurationProvider.GetOrDefaultAsync)).MakeGenericMethod(type);

            //// Assert
            
            // GetOrThrow
            await Assert.ThrowsAsync<ConfigurationNullOrEmptyException>(() => (Task)getOrThrowMethod.Invoke(configProvider, emptyValueKeyThrowArgs));
            // GetOrDefault
            Assert.Equal(defaultOfType, await (dynamic)getOrDefaultMethod.Invoke(configProvider, emptyValueKeyDefaultArgs));
            // GetOrDefault with default specified
            Assert.Equal(memberOfType, await (dynamic)getOrDefaultMethod.Invoke(configProvider, emptyValueKeyDefaultSpecifiedArgs));
        }

        private class NoConversionFromStringToThisClass
        {
        }

        [Fact]
        public async Task ThrowsForUnsupportedConversion()
        {
            // Arrange
            const string secretName = "hello i'm a secret";
            const string secretKey = "fetch me from KeyVault pls";
            
            var arguments = new Dictionary<string, string>
            {
                {secretName, secretKey}
            };

            ConfigurationProvider configProvider = new DictionaryConfigurationProvider(arguments);

            // Assert
            await Assert.ThrowsAsync<NotSupportedException>(
                async () => await configProvider.GetOrThrowAsync<NoConversionFromStringToThisClass>(secretName));
        }

        public dynamic GetDefault(Type t)
        {
            return GetType().GetMethod(nameof(GetDefaultGeneric)).MakeGenericMethod(t).Invoke(this, null);
        }

        public T GetDefaultGeneric<T>()
        {
            return default(T);
        }

        /// <summary>
        /// Used in some tests to create a member of a type.
        /// </summary>
        private readonly IDictionary<Type, object> _typeToObject = new Dictionary<Type, object>
        {
            { typeof(string), "this is a string" },
            { typeof(int), 1234 },
            { typeof(bool), true },
            { typeof(DateTime), DateTime.Now }
        };

        private static ConfigurationProvider CreateDummyConfigProvider()
        {
            return new DictionaryConfigurationProvider(new Dictionary<string, string>());
        }
    }
}