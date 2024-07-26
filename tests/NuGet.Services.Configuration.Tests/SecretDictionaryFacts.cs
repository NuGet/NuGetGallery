// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class SecretDictionaryFacts
    {
        [Fact]
        public void RefreshesSecretWhenItChanges()
        {
            // Arrange
            var mockSecretInjector = new Mock<ISecretInjector>();
            mockSecretInjector.Setup(x => x.InjectAsync(It.IsAny<string>())).Returns(Task.FromResult(Secret1.InjectedValue));

            var unprocessedDictionary = new Dictionary<string, string>()
            {
                {Secret1.Key, Secret1.Value}
            };

            var secretDict = CreateSecretDictionary(mockSecretInjector.Object, unprocessedDictionary);

            // Act
            var value1 = secretDict[Secret1.Key];
            var value2 = secretDict[Secret1.Key];

            // Assert
            mockSecretInjector.Verify(x => x.InjectAsync(It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(Secret1.InjectedValue, value1);
            Assert.Equal(value1, value2);

            // Arrange 2
            mockSecretInjector.Setup(x => x.InjectAsync(It.IsAny<string>())).Returns(Task.FromResult(Secret2.InjectedValue));

            // Act 2
            var value3 = secretDict[Secret1.Key];
            var value4 = secretDict[Secret1.Key];

            // Assert 2
            mockSecretInjector.Verify(x => x.InjectAsync(It.IsAny<string>()), Times.Exactly(4));
            Assert.Equal(Secret2.InjectedValue, value3);
            Assert.Equal(value3, value4);
        }

        [Fact]
        public void HandlesTryGet()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1 }, new List<Secret> { Secret1 });

            // Act
            string value, notFoundValue;
            var valueIsFound = secretDict.TryGetValue(Secret1.Key, out value);
            var notFoundIsFound = secretDict.TryGetValue(Secret2.Key, out notFoundValue);

            // Assert
            Assert.True(valueIsFound);
            Assert.Equal(Secret1.InjectedValue, value);
            Assert.False(notFoundIsFound);
        }

        [Fact]
        public void HandlesEnumerators()
        {
            // Arrange
            var secrets = AllSecrets;
            var secretDict = CreateSecretDictionary(secrets, secrets);
            var pairsToVerify = secrets.Select(secret => new KeyValuePair<string, string>(secret.Key, secret.InjectedValue)).ToList();

            // Act
            foreach (var pair in secretDict)
            {
                // Assert
                Assert.Contains(pair, pairsToVerify);
                pairsToVerify.Remove(pair);
            }
            Assert.Empty(pairsToVerify);
        }

        [Fact]
        public void HandlesKeys()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(AllSecrets, AllSecrets);
            var keysToVerify = AllSecrets.Select(secret => secret.Key).ToList();

            // Act
            foreach (var secretKey in secretDict.Keys)
            {
                // Assert
                Assert.Contains(secretKey, keysToVerify);
                keysToVerify.Remove(secretKey);
            }
            Assert.Empty(keysToVerify);
        }

        [Fact]
        public void HandlesValues()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(AllSecrets, AllSecrets);
            var secretsToVerify = AllSecrets.Select(secret => secret.InjectedValue).ToList();

            // Act
            foreach (var secretValue in secretDict.Values)
            {
                // Assert
                Assert.Contains(secretValue, secretsToVerify);
                secretsToVerify.Remove(secretValue);
            }
            Assert.Empty(secretsToVerify);
        }

        [Fact]
        public void HandlesKeyNotFound()
        {
            // Arrange
            const string notFoundKey = "not a real key";
            var dummy = CreateSecretDictionary();

            // Act
            string output;
            var result = dummy.TryGetValue(notFoundKey, out output);

            // Assert
            Assert.Throws<KeyNotFoundException>(() => dummy[notFoundKey]);
            Assert.False(result);
        }

        [Fact]
        public void HandlesNullArgument()
        {
            // Arrange
            var dummy = CreateSecretDictionary();

            var nullKey = "this key has a null value";
            string nullValue = null;

            // Act
            dummy.Add(nullKey, nullValue);

            // Assert
            Assert.Equal(nullValue, dummy[nullKey]);
        }

        [Fact]
        public void HandlesEmptyArgument()
        {
            // Arrange
            var dummy = CreateSecretDictionary();
            
            var emptyKey = "this key has an empty value";
            var emptyValue = "";

            // Act
            dummy.Add(emptyKey, emptyValue);

            // Assert
            Assert.Equal(emptyValue, dummy[emptyKey]);
        }

        [Fact]
        public void AddToEmptyDictionary()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1 }, new List<Secret>());

            // Act
            secretDict.Add(Secret1.Key, Secret1.Value);

            // Assert
            Assert.Single(secretDict);
            Assert.True(secretDict.ContainsKey(Secret1.Key));
            Assert.True(secretDict.Contains(new KeyValuePair<string, string>(Secret1.Key, Secret1.InjectedValue)));
            Assert.False(secretDict.Contains(new KeyValuePair<string, string>(Secret1.Key, Secret1.Value)));

            string tryget;
            Assert.True(secretDict.TryGetValue(Secret1.Key, out tryget));
            Assert.Equal(Secret1.InjectedValue, tryget);

            var result1 = secretDict[Secret1.Key];
            Assert.Equal(Secret1.InjectedValue, result1);

            Assert.Contains(new KeyValuePair<string, string>(Secret1.Key, Secret1.InjectedValue), secretDict);
            Assert.Contains(Secret1.Key, secretDict.Keys);
            Assert.Contains(Secret1.InjectedValue, secretDict.Values);
        }

        [Fact]
        public void AddToNonEmptyDictionary()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1, Secret2 }, new List<Secret> { Secret1 });

            // Act
            secretDict.Add(new KeyValuePair<string, string>(Secret2.Key, Secret2.Value));

            // Assert
            Assert.Equal(2, secretDict.Count);
            Assert.True(secretDict.ContainsKey(Secret2.Key));
            Assert.True(secretDict.Contains(new KeyValuePair<string, string>(Secret2.Key, Secret2.InjectedValue)));
            Assert.False(secretDict.Contains(new KeyValuePair<string, string>(Secret2.Key, Secret2.Value)));

            string tryget;
            Assert.True(secretDict.TryGetValue(Secret2.Key, out tryget));
            Assert.Equal(Secret2.InjectedValue, tryget);

            var result1 = secretDict[Secret2.Key];
            Assert.Equal(Secret2.InjectedValue, result1);

            Assert.Contains(new KeyValuePair<string, string>(Secret2.Key, Secret2.InjectedValue), secretDict);
            Assert.Contains(Secret2.Key, secretDict.Keys);
            Assert.Contains(Secret2.InjectedValue, secretDict.Values);
        }

        [Fact]
        public void AddAlreadyExistingSameValue()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1 }, new List<Secret> { Secret1 });
            
            // Act / Assert
            Assert.Throws<ArgumentException>(() => secretDict.Add(Secret1.Key, Secret1.Value));
            Assert.Throws<ArgumentException>(() => secretDict.Add(new KeyValuePair<string, string>(Secret1.Key, Secret1.Value)));
        }

        [Fact]
        public void AddAlreadyExistingDifferentValue()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1 }, new List<Secret> { Secret1 });
            
            // Act / Assert
            Assert.Throws<ArgumentException>(() => secretDict.Add(Secret1.Key, Secret1.InjectedValue));
            Assert.Throws<ArgumentException>(() => secretDict.Add(new KeyValuePair<string, string>(Secret1.Key, Secret1.InjectedValue)));
        }

        [Fact]
        public void RemoveNotFoundByKey()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1 }, new List<Secret> { Secret1 });

            // Act
            var result = secretDict.Remove(Secret2.Key);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveNotFoundByPair()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1 }, new List<Secret> { Secret1 });

            // Act
            var result = secretDict.Remove(new KeyValuePair<string, string>(Secret2.Key, Secret2.Value));

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveNotFoundByPairWithUninjectedValue()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1 }, new List<Secret> { Secret1 });

            // Act
            var result = secretDict.Remove(new KeyValuePair<string, string>(Secret1.Key, Secret1.Value));

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveByKey()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1, Secret2 }, new List<Secret> { Secret1, Secret2 });

            // Act
            var result = secretDict.Remove(Secret2.Key);

            // Assert
            Assert.True(result);

            Assert.Single(secretDict);
            Assert.False(secretDict.ContainsKey(Secret2.Key));
            Assert.False(secretDict.Contains(new KeyValuePair<string, string>(Secret2.Key, Secret2.InjectedValue)));
            Assert.False(secretDict.Contains(new KeyValuePair<string, string>(Secret2.Key, Secret2.Value)));
            Assert.False(secretDict.Contains(new KeyValuePair<string, string>(Secret2.Key, Secret1.Value)));

            string tryget;
            Assert.False(secretDict.TryGetValue(Secret2.Key, out tryget));

            Assert.Throws<KeyNotFoundException>(() => secretDict[Secret2.Key]);

            Assert.DoesNotContain(new KeyValuePair<string, string>(Secret2.Key, Secret2.InjectedValue), secretDict);
            Assert.DoesNotContain(Secret2.Key, secretDict.Keys);
            Assert.DoesNotContain(Secret2.InjectedValue, secretDict.Values);
        }

        [Fact]
        public void RemoveByPair()
        {
            // Arrange
            var secretDict = CreateSecretDictionary(new List<Secret> { Secret1 }, new List<Secret> { Secret1 });

            // Act
            var result = secretDict.Remove(new KeyValuePair<string, string>(Secret1.Key, Secret1.InjectedValue));

            // Assert
            Assert.True(result);

            Assert.Empty(secretDict);
            Assert.False(secretDict.ContainsKey(Secret1.Key));
            Assert.False(secretDict.Contains(new KeyValuePair<string, string>(Secret1.Key, Secret1.InjectedValue)));

            string tryget;
            Assert.False(secretDict.TryGetValue(Secret1.Key, out tryget));

            Assert.Throws<KeyNotFoundException>(() => secretDict[Secret1.Key]);

            Assert.DoesNotContain(new KeyValuePair<string, string>(Secret1.Key, Secret1.InjectedValue), secretDict);
            Assert.DoesNotContain(Secret1.Key, secretDict.Keys);
            Assert.DoesNotContain(Secret1.InjectedValue, secretDict.Values);
        }

        [Fact]
        public void NotInjectedKeys()
        {
            // Arrange
            var key = "someKey";
            var value = "someValue";

            var unprocessedDictionary = new Dictionary<string, string>()
            {
                { key, value }
            };
            var notInjectedKeys = new HashSet<string> { key };

            var mockSecretInjector = new Mock<ISecretInjector>();
            mockSecretInjector.Setup(x => x.InjectAsync(It.IsAny<string>()));

            var secretDict = CreatSecretDictionaryWithNotInjectedKeys(mockSecretInjector.Object,
                unprocessedDictionary,
                notInjectedKeys);

            // Act and Assert 1
            var dictValue = secretDict[key];
            Assert.Equal(value, dictValue);

            // Act and Assert 2
            Assert.True(secretDict.TryGetValue(key, out var tryget));
            Assert.Equal(value, tryget);

            // Act and Assert 3
            var dictValues = secretDict.Values;
            Assert.Single(dictValues);
            Assert.Equal(value, dictValues.First());

            // Act and Assert 4
            Assert.True(secretDict.Contains(new KeyValuePair<string, string>(key, value)));

            // Act and Assert 5
            foreach (var pair in secretDict)
            {
                Assert.Equal(key, pair.Key);
                Assert.Equal(value, pair.Value);
            }

            // Act and Assert 6
            Assert.True(secretDict.Remove(key));

            mockSecretInjector.Verify(x => x.InjectAsync(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Utility class that allows construction of tests more easily.
        /// </summary>
        private class Secret
        {
            public string Key { get; }

            public string Value { get; }

            public string InjectedValue { get; }

            public Secret(string key, string value, string injectedValue)
            {
                Key = key;
                Value = value;
                InjectedValue = injectedValue;
            }
        }

        // Constant Secrets for tests
        private static Secret Secret1 => new Secret("a", "1", "!");
        private static Secret Secret2 => new Secret("b", "2", "@");
        private static Secret Secret3 => new Secret("c", "3", "#");
        private static ICollection<Secret> AllSecrets => new List<Secret> {Secret1, Secret2, Secret3};

        private static IDictionary<string, string> CreateSecretDictionary()
        {
            return new SecretDictionary(new SecretInjector(new EmptySecretReader()), new Dictionary<string, string>());
        }

        private static IDictionary<string, string> CreateSecretDictionary(ISecretInjector secretInjector, IDictionary<string, string> unprocessedArgs)
        {
            return new SecretDictionary(secretInjector, unprocessedArgs);
        }

        private static IDictionary<string, string> CreatSecretDictionaryWithNotInjectedKeys(ISecretInjector secretInjector,
            IDictionary<string, string> unprocessedArgs,
            HashSet<string> notInjectedKeys)
        {
            return new SecretDictionary(secretInjector, unprocessedArgs, notInjectedKeys);
        }

        private static Mock<ISecretInjector> CreateMappedSecretInjectorMock(IDictionary<string, string> keyToValue)
        {
            var mockSecretInjector = new Mock<ISecretInjector>();
            mockSecretInjector.Setup(x => x.InjectAsync(It.IsAny<string>())).Returns<string>(key => Task.FromResult(keyToValue[key]));
            return mockSecretInjector;
        }
        
        private static IDictionary<string, string> CreateSecretDictionary(ICollection<Secret> secretsToMap,
            ICollection<Secret> secretsToInclude)
        {
            var unprocessedDictionary = secretsToMap
                .Where(secret => secretsToInclude.Select(secretToInclude => secretToInclude.Key).Any(key => key == secret.Key))
                .ToDictionary(secret => secret.Key, secret => secret.Value);

            var valueToInjectedValue = secretsToMap.ToDictionary(secret => secret.Value, secret => secret.InjectedValue);

            var mockSecretInjector = CreateMappedSecretInjectorMock(valueToInjectedValue);

            return CreateSecretDictionary(mockSecretInjector.Object, unprocessedDictionary);
        }
    }
}
