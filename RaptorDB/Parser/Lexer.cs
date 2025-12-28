using System;
using System.Collections.Generic;
using System.Text;

namespace RaptorDB.RaptorDB.Parser
{
    internal class Lexer
    {
        private readonly string _input;
        private int _position = 0;

        public Lexer(string input)
        {
            _input = input.Trim();
        }

        public List<string> Tokenize()
        {
            var tokens = new List<string>();

            while (_position < _input.Length)
            {
                char current = _input[_position];

                if (char.IsWhiteSpace(current))
                {
                    _position++;
                    continue;
                }

                if (current == ';')
                {
                    _position++;
                    continue;
                }

                // --- HANDLE OPERATORS (>=, <=, !=, <, >, =) ---
                if ("=<>!".Contains(current))
                {
                    // Check for 2-char operators
                    if (_position + 1 < _input.Length && _input[_position + 1] == '=')
                    {
                        tokens.Add(current.ToString() + "="); // >=, <=, !=, ==
                        _position += 2;
                    }
                    else
                    {
                        tokens.Add(current.ToString()); // <, >, =
                        _position++;
                    }
                    continue;
                }

                if ("(),+-*".Contains(current))
                {
                    tokens.Add(current.ToString());
                    _position++;
                    continue;
                }

                if (char.IsLetter(current) || current == '_')
                {
                    tokens.Add(ReadIdentifier());
                    continue;
                }

                if (char.IsDigit(current))
                {
                    tokens.Add(ReadNumberOrDate());
                    continue;
                }

                if (current == '\'' || current == '"')
                {
                    tokens.Add(ReadQuoted());
                    continue;
                }

                _position++;
            }

            return tokens;
        }

        private string ReadIdentifier()
        {
            int start = _position;
            while (_position < _input.Length &&
                  (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
            {
                _position++;
            }
            return _input.Substring(start, _position - start);
        }

        private string ReadNumberOrDate()
        {
            int start = _position;
            while (_position < _input.Length)
            {
                char c = _input[_position];
                if (char.IsDigit(c) || c == '.' || c == ':' || c == '-' || c == 'T')
                    _position++;
                else
                    break;
            }
            return _input.Substring(start, _position - start);
        }

        private string ReadQuoted()
        {
            char quote = _input[_position++];
            StringBuilder sb = new StringBuilder();
            while (_position < _input.Length)
            {
                char c = _input[_position];
                if (c == quote) { _position++; break; }
                sb.Append(c);
                _position++;
            }
            return sb.ToString();
        }
    }
}