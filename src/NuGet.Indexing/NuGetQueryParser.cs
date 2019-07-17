// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Indexing
{
    public class NuGetQueryParser
    {
        private readonly Dictionary<QueryField, string[]> _queryFieldNames = new Dictionary<QueryField, string[]>
        {
            { QueryField.Id,  new [] { "id" } },
            { QueryField.PackageId,  new [] { "packageid" } },
            { QueryField.Version,  new [] { "version" } },
            { QueryField.Title,  new [] { "title" } },
            { QueryField.Description,  new [] { "description" } },
            { QueryField.Tag,  new [] { "tag", "tags" } },
            { QueryField.Author,  new [] { "author", "authors" } },
            { QueryField.Summary,  new [] { "summary" } },
            { QueryField.Owner,  new [] { "owner", "owners" } },
            { QueryField.Any,  new [] { "*" } }
        };

        public Dictionary<QueryField, HashSet<string>> ParseQuery(string query, bool skipWhiteSpace = false)
        {
            var grouping = new Dictionary<QueryField, HashSet<string>>();
            foreach (Clause clause in MakeClauses(Tokenize(query)))
            {
                if (skipWhiteSpace && string.IsNullOrWhiteSpace(clause.Text))
                {
                    continue;
                }

                HashSet<string> text;
                var queryField = GetQueryField(clause.Field);
                if (!grouping.TryGetValue(queryField, out text))
                {
                    text = new HashSet<string>();
                    grouping.Add(queryField, text);
                }
                text.Add(clause.Text);
            }

            return grouping;
        }

        private QueryField GetQueryField(string field)
        {
            foreach (var queryFieldName in _queryFieldNames)
            {
                if (queryFieldName.Value.Any(
                    s => string.Compare(s, field, StringComparison.InvariantCultureIgnoreCase) == 0))
                {
                    return queryFieldName.Key;
                }
            }

            return QueryField.Invalid;
        }

        private static IEnumerable<Clause> MakeClauses(IEnumerable<Token> tokens)
        {
            string field = null;

            foreach (Token token in tokens)
            {
                if (token.Type == Token.TokenType.Keyword)
                {
                    field = token.Value;
                }
                else if (token.Type == Token.TokenType.Value)
                {
                    if (token.Value.ToLowerInvariant() == "and" || token.Value.ToLowerInvariant() == "or")
                    {
                        continue;
                    }
                    if (field != null)
                    {
                        yield return new Clause { Field = field, Text = token.Value };
                    }
                    else
                    {
                        yield return new Clause { Field = "*", Text = token.Value };
                    }
                    field = null;
                }
            }

            yield break;
        }

        private static IEnumerable<Token> Tokenize(string s)
        {
            var buf = new StringBuilder();

            int state = 0;
            bool previousTokenIsKeyword = false;

            foreach (char ch in s)
            {
                switch (state)
                {
                case 0:
                    if (Char.IsWhiteSpace(ch))
                    {
                        if (buf.Length > 0)
                        {
                            yield return new Token { Type = Token.TokenType.Value, Value = buf.ToString() };
                            previousTokenIsKeyword = false;
                            buf.Clear();
                        }
                    }
                    else if (ch == '"')
                    {
                        state = 1;
                    }
                    else if (ch == ':')
                    {
                        if (buf.Length > 0)
                        {
                            yield return new Token { Type = Token.TokenType.Keyword, Value = buf.ToString() };
                            previousTokenIsKeyword = true;
                            buf.Clear();
                        }
                    }
                    else
                    {
                        buf.Append(ch);
                    }
                    break;
                case 1:
                    if (ch == '"')
                    {
                        if (buf.Length > 0 || previousTokenIsKeyword)
                        {
                            yield return new Token { Type = Token.TokenType.Value, Value = buf.ToString() };
                            previousTokenIsKeyword = false;
                            buf.Clear();
                        }
                        state = 0;
                    }
                    else
                    {
                        buf.Append(ch);
                    }
                    break;
                }
            }

            if (buf.Length > 0)
            {
                yield return new Token { Type = Token.TokenType.Value, Value = buf.ToString() };
                previousTokenIsKeyword = false;
            }

            yield break;
        }

        private class Token
        {
            public enum TokenType { Value, Keyword }
            public TokenType Type { get; set; }
            public string Value { get; set; }
        }

        private class Clause
        {
            public string Field { get; set; }
            public string Text { get; set; }
        }
    }
}