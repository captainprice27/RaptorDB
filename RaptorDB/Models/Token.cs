using System;

namespace RaptorDB.RaptorDB.Models
{
    /// <summary>
    /// Represents a single token produced by the lexer.
    /// A token is the smallest meaningful unit in a query (keyword, identifier, number, string, symbol).
    /// </summary>
    internal class Token
    {
        /// <summary>
        /// The literal text value of the token (e.g., "PUT", "users", "(", "John").
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Type/category of the token for parser use (optional extension).
        /// </summary>
        public TokenType Type { get; }

        public Token(string value, TokenType type)
        {
            Value = value;
            Type = type;
        }
    }

    /// <summary>
    /// The type classification of a token. (Extend later as needed.)
    /// </summary>
    internal enum TokenType
    {
        Keyword,
        Identifier,
        Number,
        StringLiteral,
        Symbol
    }
}
