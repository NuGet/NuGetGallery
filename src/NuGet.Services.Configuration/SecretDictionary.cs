// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    public class SecretDictionary : IDictionary<string, string>
    {
        private readonly ISecretInjector _secretInjector;
        private readonly IDictionary<string, string> _unprocessedArguments;

        public SecretDictionary(ISecretInjector secretInjector, IDictionary<string, string> unprocessedArguments)
        {
            _secretInjector = secretInjector;
            _unprocessedArguments = unprocessedArguments;
        }

        private string Inject(string key)
        {
            return _secretInjector.InjectAsync(key).Result;
        }

        public string this[string key]
        {
            get { return Inject(_unprocessedArguments[key]); }
            set { _unprocessedArguments[key] = value; }
        }

        public bool TryGetValue(string key, out string value)
        {
            string unprocessedValue;
            var isFound = _unprocessedArguments.TryGetValue(key, out unprocessedValue);
            value = isFound ? Inject(unprocessedValue) : null;
            return isFound;
        }

        public ICollection<string> Values => _unprocessedArguments.Values.Select(Inject).ToList();

        public class SecretEnumerator : IEnumerator<KeyValuePair<string, string>>
        {
            private readonly ISecretInjector _secretInjector;
            private readonly IList<KeyValuePair<string, string>> _unprocessedPairs;

            private int _position = -1;

            public SecretEnumerator(ISecretInjector secretInjector, IDictionary<string, string> unprocessedArguments)
            {
                _secretInjector = secretInjector;
                _unprocessedPairs = unprocessedArguments.ToList();
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                _position++;
                return _position < _unprocessedPairs.Count;
            }

            public void Reset()
            {
                _position = -1;
            }

            private KeyValuePair<string, string> Inject(KeyValuePair<string, string> pair) => 
                new KeyValuePair<string, string>(pair.Key, _secretInjector.InjectAsync(pair.Value).Result);

            public KeyValuePair<string, string> Current => Inject(_unprocessedPairs[_position]);

            object IEnumerator.Current => Current;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => new SecretEnumerator(_secretInjector, _unprocessedArguments);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Contains(KeyValuePair<string, string> item)
        {
            return ContainsKey(item.Key) && Inject(_unprocessedArguments[item.Key]) == item.Value;
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return Contains(item) && _unprocessedArguments.Remove(item.Key);
        }

        #region Wrapper interface methods

        public int Count => _unprocessedArguments.Count;

        public bool IsReadOnly => _unprocessedArguments.IsReadOnly;

        public ICollection<string> Keys => _unprocessedArguments.Keys;

        public void Add(KeyValuePair<string, string> item) => _unprocessedArguments.Add(item);

        public void Add(string key, string value) => _unprocessedArguments.Add(key, value);

        public void Clear() => _unprocessedArguments.Clear();

        public bool ContainsKey(string key) => _unprocessedArguments.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
            => _unprocessedArguments.CopyTo(array, arrayIndex);

        public bool Remove(string key) => _unprocessedArguments.Remove(key);

        #endregion

    }
}
