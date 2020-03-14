// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    public class SecretDictionary : IDictionary<string, string>
    {
        private readonly ISecretInjector _secretInjector;
        private readonly IDictionary<string, string> _unprocessedArguments;
        private readonly HashSet<string> _notInjectedKeys;

        public SecretDictionary(ISecretInjector secretInjector, IDictionary<string, string> unprocessedArguments,
            HashSet<string> notInjectedKeys) : this(secretInjector, unprocessedArguments)
        {
            _notInjectedKeys = notInjectedKeys;
        }

        public SecretDictionary(ISecretInjector secretInjector, IDictionary<string, string> unprocessedArguments)
        {
            _secretInjector = secretInjector;
            _unprocessedArguments = unprocessedArguments;
            _notInjectedKeys = new HashSet<string>();
        }

        public string this[string key]
        {
            get { return InjectOrSkip(key, _unprocessedArguments[key]); }
            set { _unprocessedArguments[key] = value; }
        }

        public bool TryGetValue(string key, out string value)
        {
            string unprocessedValue;
            var isFound = _unprocessedArguments.TryGetValue(key, out unprocessedValue);
            value = isFound ? InjectOrSkip(key, unprocessedValue) : null;
            return isFound;
        }

        public ICollection<string> Values => _unprocessedArguments.Select(p => InjectOrSkip(p.Key, p.Value)).ToList();

        private class SecretEnumerator : IEnumerator<KeyValuePair<string, string>>
        {
            private readonly Func<string, string, string> _injectOrSkipFunc;
            private readonly IList<KeyValuePair<string, string>> _unprocessedPairs;

            private int _position = -1;

            public SecretEnumerator(Func<string, string, string> injectOrSkipFunc, IDictionary<string, string> unprocessedArguments)
            {
                _injectOrSkipFunc = injectOrSkipFunc;
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

            public KeyValuePair<string, string> Current => Inject(_unprocessedPairs[_position]);

            private KeyValuePair<string, string> Inject(KeyValuePair<string, string> pair) =>
                new KeyValuePair<string, string>(pair.Key, _injectOrSkipFunc(pair.Key, pair.Value));

            object IEnumerator.Current => Current;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => new SecretEnumerator(InjectOrSkip, _unprocessedArguments);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Contains(KeyValuePair<string, string> item)
        {
            return ContainsKey(item.Key) && InjectOrSkip(item.Key, _unprocessedArguments[item.Key]) == item.Value;
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

        private string InjectOrSkip(string key, string value)
        {
            if (!_notInjectedKeys.Contains(key))
            {
                return Inject(value).Result;
            }
            return value;
        }

        private Task<string> Inject(string value)
        {
            return _secretInjector.InjectAsync(value);
        }
    }
}
