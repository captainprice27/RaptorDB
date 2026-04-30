namespace RaptorDB.RaptorDB.Parser.AST
{
    /// <summary>
    /// Type of JOIN to perform between the FROM table (left) and the JOIN table (right).
    /// Added in v2.0 — supports INNER, LEFT, and RIGHT joins via Nested-Loop algorithm.
    /// </summary>
    internal enum JoinType
    {
        Inner,
        Left,
        Right
    }

    /// <summary>
    /// Represents a single JOIN ... ON clause attached to a SELECT statement.
    ///
    ///   SELECT * FROM students s
    ///   INNER JOIN courses c ON students.id = courses.student_id;
    ///
    /// LeftColumn / RightColumn are stored as fully-qualified "table.column"
    /// identifiers as written by the user. The execution engine resolves which
    /// side of the join each refers to.
    ///
    /// Note: RaptorDB has no native NULL storage. For LEFT/RIGHT joins, the
    /// engine emits a synthetic "&lt;NULL&gt;" placeholder for unmatched rows
    /// in the projected output only — nothing is ever written to disk.
    /// </summary>
    internal class JoinClause
    {
        public JoinType Type { get; }
        public string TableName { get; }
        public string LeftColumn { get; }
        public string RightColumn { get; }

        public JoinClause(JoinType type, string tableName, string leftColumn, string rightColumn)
        {
            Type = type;
            TableName = tableName;
            LeftColumn = leftColumn;
            RightColumn = rightColumn;
        }
    }
}
