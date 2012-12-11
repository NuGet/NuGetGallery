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
            while (_tokenizer.Peek() != Tok.Eof)
            {
                var term = new NuGetSearchTerm();
                if (_tokenizer.Peek() == Tok.Field)
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
            while (_tokenizer.Peek() == Tok.Field);

            // Eof, Term, or Phrase....
            if (_tokenizer.Peek() != Tok.Eof)
            {
                return ParseTermOrPhrase(result);
            }

            return false;
        }

        public bool ParseTermOrPhrase(NuGetSearchTerm result)
        {
            Debug.Assert(_tokenizer.Peek() == Tok.Term || _tokenizer.Peek() == Tok.Phrase);
            result.TermOrPhrase = _tokenizer.Term() ?? _tokenizer.Phrase();
            _tokenizer.Pop();
            return true;
        }

        enum Tok
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
            private Tok _tok;
            private string _next;

            public Tokenizer(string s)
            {
                _s = s;
                _p = 0;
                _tok = Tok.Null;
                Scan();
            }

            public Tok Peek()
            {
                return _tok;
            }

            public string Field()
            {
                return _tok == Tok.Field ? _next : null;
            }

            public string Term()
            {
                return _tok == Tok.Term ? _next : null;
            }

            public string Phrase()
            {
                return _tok == Tok.Phrase ? _next : null;
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
                    _tok = Tok.Eof;
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
                        _tok = Tok.Phrase;
                        _next = s.Substring(i + 1);
                        _p = s.Length;
                        return;
                    }

                    _tok = Tok.Phrase;
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
                            _tok = Tok.Term;
                            _next = s.Substring(i, k - i);
                            _p = k;
                            return;
                        }
                        else if (s[k] == ':')
                        {
                            _tok = Tok.Field;
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