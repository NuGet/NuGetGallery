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
        public List<string[]> Parse(string searchTerm)
        {
            List<string[]> ret = new List<string[]>();
            _tokenizer = new Tokenizer(searchTerm);
            while (_tokenizer.Peek() != Tok.Eof)
            {
                string[] term = new string[2];
                if (_tokenizer.Peek() == Tok.Field)
                {
                    if (ParseField(term)) { ret.Add(term); }
                }
                else
                {
                    if (ParseTermOrPhrase(term)) { ret.Add(term); }
                }
            }

            return ret;
        }

        public bool ParseField(string[] result)
        {
            // ignore extra leading fields - just accept the last one
            do
            {
                result[0] = _tokenizer.Field();
                _tokenizer.Pop();
            } while (_tokenizer.Peek() == Tok.Field);

            // Eof, Term, or Phrase....
            if (_tokenizer.Peek() != Tok.Eof)
            {
                return ParseTermOrPhrase(result);
            }

            return false;
        }

        public bool ParseTermOrPhrase(string[] result)
        {
            Debug.Assert(_tokenizer.Peek() == Tok.Term || _tokenizer.Peek() == Tok.Phrase);
            result[1] = _tokenizer.Term() ?? _tokenizer.Phrase();
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
            string _s;
            int _p;
            Tok _tok;
            string _next;

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

                try
                {
                    // Skip whitespace
                    // Skip syntax error of leading colons
                    while (Char.IsWhiteSpace(s[i]) || s[i] == ':') { i += 1; }

                    if (s[i] == '"')
                    {
                        // phrase
                        int j = i + 1;
                        while (s[j] != '"') { j += 1; }

                        _tok = Tok.Phrase;
                        _next = s.Substring(i + 1, j - i - 1);
                        _p = j + 1;
                    }
                    else
                    {
                        try
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
                                    break;
                                }
                                else if (s[k] == ':')
                                {
                                    _tok = Tok.Field;
                                    _next = s.Substring(i, k - i);
                                    _p = k + 1;
                                    break;
                                }
                            }
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Debug.Assert(false, "should never get index out of range exception in this loop");
                        }
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // Eof - were we iterating with j in a string? Just test i.
                    if (i < s.Length)
                    {
                        _tok = Tok.Phrase;
                        _next = s.Substring(i + 1);
                        _p = s.Length;
                    }
                    else
                    {
                        // Eof while reading s[i]
                        _tok = Tok.Eof;
                        _next = null;
                    }
                }
            }
        }
    }
}