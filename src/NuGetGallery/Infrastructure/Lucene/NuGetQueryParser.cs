using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGetGallery
{
    // Justification - a custom query parser is better to gain control over queries so that
    // evildoers cannot use knowledge of Lucene to hit the index with extremely non-performant queries.
    //
    // This parser is pretty simple - it understands named fields, phrases, and terms.
    // Reserved characters in searches are colon and quote (':' and '"').
    public class NuGetQueryParser
    {
        Tokenizer _tokenizer;

        // Returns list of 2-element arrays, element 0 is field name (or null), element 1 is term/phrase
        public List<NuGetSearchTerm> Parse(string searchTerm)
        {
            var terms = new List<NuGetSearchTerm>();
            _tokenizer = new Tokenizer(searchTerm);
            while (_tokenizer.Peek() != TokenType.Eof)
            {
                var term = new NuGetSearchTerm();
                if (_tokenizer.Peek() == TokenType.Field)
                {
                    if (ParseField(term))
                    {
                        terms.Add(term);
                    }
                }
                else
                {
                    if (ParseTermOrPhrase(term))
                    {
                        terms.Add(term);
                    }
                }
            }

            return terms;
        }

        public bool ParseField(NuGetSearchTerm result)
        {
            // ignore extra leading fields - just accept the last one
            do
            {
                result.Field = _tokenizer.Field();
                _tokenizer.Pop();
            }
            while (_tokenizer.Peek() == TokenType.Field);

            // Eof, Term, or Phrase....
            if (_tokenizer.Peek() != TokenType.Eof)
            {
                return ParseTermOrPhrase(result);
            }

            return false;
        }

        public bool ParseTermOrPhrase(NuGetSearchTerm result)
        {
            Debug.Assert(_tokenizer.Peek() == TokenType.Term || _tokenizer.Peek() == TokenType.Phrase);
            result.TermOrPhrase = _tokenizer.Term() ?? _tokenizer.Phrase();
            _tokenizer.Pop();
            return true;
        }

        enum TokenType
        {
            Null = 0,
            Field = 1,
            Term = 2,
            Phrase = 3,
            Eof = 4,
        }

        class Tokenizer
        {
            private readonly string _s;
            private int _p;
            private TokenType _tokenType;
            private string _next;

            public Tokenizer(string s)
            {
                _s = s;
                _p = 0;
                _tokenType = TokenType.Null;
                Scan();
            }

            public TokenType Peek()
            {
                return _tokenType;
            }

            public string Field()
            {
                return _tokenType == TokenType.Field ? _next : null;
            }

            public string Term()
            {
                return _tokenType == TokenType.Term ? _next : null;
            }

            public string Phrase()
            {
                return _tokenType == TokenType.Phrase ? _next : null;
            }

            public Tokenizer Pop()
            {
                Scan();
                return this;
            }

            private void Scan()
            {
                int i = _p;
                string s = _s;

                // Possible states to detect/handle:
                // -Eof
                // -Whitespace
                // -Field
                // -Quoted phrase
                // -Unquoted term

                // START
                // Skip whitespace
                // Skip syntax error of leading colons
                while (i < s.Length && (Char.IsWhiteSpace(s[i]) || s[i] == ':'))
                {
                    i += 1;
                }
                
                if (i >= s.Length)
                {
                    // Eof while reading s[i]
                    _tokenType = TokenType.Eof;
                    _next = null;
                    return;
                }

                if (s[i] == '"')
                {
                    // phrase
                    int j = i + 1;
                    while (j < s.Length && s[j] != '"')
                    {
                        j += 1;
                    }

                    // Eof while reading s[j]
                    if (i >= s.Length)
                    {
                        _tokenType = TokenType.Phrase;
                        _next = s.Substring(i + 1);
                        _p = s.Length;
                        return;
                    }

                    _tokenType = TokenType.Phrase;
                    _next = s.Substring(i + 1, j - i - 1);
                    _p = j + 1;
                    return;
                }
                else
                {
                    // field, or unquoted term, look ahead to see what comes first - colon, whitespace, or eof
                    int k;
                    for (k = i; k <= s.Length; k++)
                    {
                        if (k == s.Length || Char.IsWhiteSpace(s[k]))
                        {
                            _tokenType = TokenType.Term;
                            _next = s.Substring(i, k - i);
                            _p = k;
                            return;
                        }
                        else if (s[k] == ':')
                        {
                            _tokenType = TokenType.Field;
                            _next = s.Substring(i, k - i);
                            _p = k + 1;
                            return;
                        }
                    }
                }
            }
        }
    }
}