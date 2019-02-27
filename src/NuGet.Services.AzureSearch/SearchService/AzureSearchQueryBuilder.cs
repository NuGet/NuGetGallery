// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.AzureSearch.SearchService
{
    /// <summary>
    /// Used to build Azure Search Service queries. Used by <see cref="SearchTextBuilder"/>.
    /// Given the query "fieldA:value1 value2":
    /// 
    ///   * "value1" is a field-scoped value 
    ///   * "value2" is a non-field scoped value
    /// </summary>
    internal class AzureSearchQueryBuilder
    {
        /// <summary>
        /// Azure Search Queries must have less than 1024 clauses.
        /// See: https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax#bkmk_querysizelimits
        /// </summary>
        private const int MaxClauses = 1024;

        /// <summary>
        /// Terms in Azure Search Queries must be less than 32KB.
        /// See: https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax#bkmk_querysizelimits
        /// </summary>
        private const int MaxTermSizeBytes = 32 * 1024;

        /// <summary>
        /// These characters have special meaning in Azure Search and must be escaped if in user input.
        /// See: https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax#escaping-special-characters
        /// </summary>
        private static readonly HashSet<char> SpecialCharacters = new HashSet<char>
        {
            '+', '-', '&', '|', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '\\', '/',
        };

        private readonly List<string> _nonFieldScopedValues;
        private readonly Dictionary<string, List<string>> _fieldScopedValues;

        public AzureSearchQueryBuilder()
        {
            _nonFieldScopedValues = new List<string>();
            _fieldScopedValues = new Dictionary<string, List<string>>();
        }

        public void AddNonFieldScopedValues(IEnumerable<string> values)
        {
            _nonFieldScopedValues.AddRange(values);
        }

        public void AddFieldScopedValues(string fieldName, IEnumerable<string> values)
        {
            if (!_fieldScopedValues.ContainsKey(fieldName))
            {
                _fieldScopedValues[fieldName] = new List<string>();
            }

            _fieldScopedValues[fieldName].AddRange(values);
        }

        public override string ToString()
        {
            ValidateOrThrow();

            var result = new StringBuilder();

            foreach (var fieldScopedTerm in _fieldScopedValues)
            {
                // At least one term from each field-scope must be matched. As Azure Search queries have an implicit "OR" between
                // clauses, we must mark field-scoped term as required if there are multiple top-level clauses.
                if (result.Length == 0)
                {
                    // We are building the query's first clause, only add the required operator "+" if there are other top-level clauses.
                    // We generate a top-level clause for each non-field-scoped term and one for each field-scopes.
                    if (_nonFieldScopedValues.Count > 0 || _fieldScopedValues.Keys.Count > 1)
                    {
                        result.Append('+');
                    }
                }
                else
                {
                    // We are adding another top-level clause to the query, always add the required operator "+".
                    result.Append(" +");
                }

                result.Append(fieldScopedTerm.Key);
                result.Append(':');

                if (fieldScopedTerm.Value.Count == 1)
                {
                    AppendEscapedString(result, fieldScopedTerm.Value[0]);
                }
                else
                {
                    result.Append('(');
                    AppendEscapedValues(result, fieldScopedTerm.Value);
                    result.Append(')');
                }
            }

            if (_nonFieldScopedValues.Any())
            {
                if (result.Length > 0)
                {
                    result.Append(' ');
                }

                AppendEscapedValues(result, _nonFieldScopedValues);
            }

            return result.ToString();
        }

        private static void AppendEscapedValues(StringBuilder result, IReadOnlyList<string> values)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    result.Append(' ');
                }

                AppendEscapedString(result, values[i]);
            }
        }

        private static void AppendEscapedString(StringBuilder result, string input)
        {
            var originalLength = result.Length;

            var wrapWithQuotes = input.Any(char.IsWhiteSpace);
            if (wrapWithQuotes)
            {
                result.Append('"');
            }

            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (SpecialCharacters.Contains(c))
                {
                    if (originalLength == result.Length)
                    {
                        result.Append(input.Substring(0, i));
                    }

                    result.Append('\\');
                    result.Append(c);
                }
                else if (result.Length != originalLength)
                {
                    result.Append(c);
                }
            }

            if (wrapWithQuotes)
            {
                result.Append('"');
            }

            if (result.Length == originalLength)
            {
                result.Append(input);
            }
        }

        private void ValidateOrThrow()
        {
            // Azure Search has a limit on the number of clauses in a single query.
            // We generate a clause for each value in a field-scope, each field-scope,
            // and each non-field-scoped value.
            var fieldScopedClauses = _fieldScopedValues.Sum(CountFieldScopedClauses);
            var nonFieldScopedClauses = _nonFieldScopedValues.Count;

            if ((fieldScopedClauses + nonFieldScopedClauses) > MaxClauses)
            {
                throw new InvalidOperationException($"A query can only have up to {MaxClauses} clauses");
            }

            if (_fieldScopedValues.Values.Any(terms => terms.Any(TermExceedsMaxSize))
                || _nonFieldScopedValues.Any(TermExceedsMaxSize))
            {
                throw new InvalidOperationException($"Query terms cannot exceed {MaxTermSizeBytes} bytes");
            }
        }

        private static int CountFieldScopedClauses(KeyValuePair<string, List<string>> fieldScopedValues)
        {
            // We will only generate a single clause if this field-scope only has a single term.
            if (fieldScopedValues.Value.Count == 1)
            {
                return 1;
            }

            // Otherwise, we will generate a clause for each term and a clause to OR the terms together.
            return fieldScopedValues.Value.Count + 1;
        }

        private static bool TermExceedsMaxSize(string term)
        {
            return (Encoding.Unicode.GetByteCount(term) > MaxTermSizeBytes);
        }
    }
}
