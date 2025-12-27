using System;
using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Parser
{
    internal class Lexer
    {
        private string _input;
        private int _position = 0;

        public Lexer(string input)
        {
            // Normalize weird user input patterns
            _input = input
                .Replace("SELECT*", "SELECT *")
                .Replace("select*", "select *")
                .Trim();
        }

        public List<string> Tokenize()
        {
            var tokens = new List<string>();

            while (_position < _input.Length)
            {
                char current = _input[_position];

                // Ignore whitespace
                if (char.IsWhiteSpace(current))
                {
                    _position++;
                    continue;
                }

                // Ignore semicolons → ensures "school;" becomes "school"
                if (current == ';')
                {
                    _position++;
                    continue;
                }

                // Identifiers/table/columns/keywords
                if (char.IsLetter(current))
                {
                    tokens.Add(ReadIdentifier().ToLower().TrimEnd(';'));
                    continue;
                }

                // Numbers / Dates / DateTime
                if (char.IsDigit(current))
                {
                    tokens.Add(ReadDateOrNumber().TrimEnd(';'));
                    continue;
                }

                // Strings "Aman" or 'Aman'
                if (current == '"' || current == '\'')
                {
                    tokens.Add(ReadQuoted().TrimEnd(';'));
                    continue;
                }

                // Symbols
                if ("(),=+-*".Contains(current))
                {
                    tokens.Add(current.ToString());
                    _position++;
                    continue;
                }

                _position++; // fallback skip
            }

            return tokens;
        }

        // ----------------------------------------------------------------------
        // HELPERS
        // ----------------------------------------------------------------------

        private string ReadIdentifier()
        {
            int start = _position;
            while (_position < _input.Length &&
                  (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
                _position++;

            return _input.Substring(start, _position - start).Trim();
        }

        private string ReadDateOrNumber()
        {
            int start = _position;

            while (_position < _input.Length)
            {
                char c = _input[_position];

                // Accept digits and date/time chars
                if (char.IsDigit(c) || c == '-' || c == ':' || c == '.' || c == 'T' || c == ' ')
                {
                    _position++;
                    continue;
                }

                break;
            }

            return _input.Substring(start, _position - start).Trim();
        }

        private string ReadQuoted()
        {
            char quote = _input[_position++];
            int start = _position;

            while (_position < _input.Length && _input[_position] != quote)
                _position++;

            string val = _input.Substring(start, _position - start);
            _position++;

            return val.Trim();
        }
    }
}
