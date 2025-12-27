using System;
using System.Collections.Generic;
using RaptorDB.RaptorDB.Parser.AST;

namespace RaptorDB.RaptorDB.Core
{
    /// <summary>
    /// The QueryPlanner is responsible for deciding *how* a query should execute.
    /// It acts as a decision layer between the Parser/AST and the ExecutionEngine.
    /// 
    /// Current behavior (Phase 1 Prototype):
    ///  - Always returns a basic execution strategy
    ///  - No index usage yet, because schema + index system is not integrated
    /// 
    /// Future behavior (Phase 2+):
    ///  - Detect available indexes from schema and .idx files
    ///  - Choose fastest strategy (index lookup vs full scan)
    ///  - Integrate cost-based planning and statistics
    ///  - Push-down filtering and projection optimization
    /// </summary>
    internal class QueryPlanner
    {
        /// <summary>
        /// Generates an execution plan for a given AST node.
        /// A "plan" is currently just a text hint for the ExecutionEngine,
        /// but will evolve into structured plan trees.
        /// 
        /// Example return values:
        ///  "FULL_SCAN"
        ///  "USE_INDEX:id"
        ///  "NO_WHERE_CLAUSE"
        /// </summary>
        /// <param name="node">The AST command to analyze.</param>
        /// <returns>A query plan string describing the strategy.</returns>
        public string Plan(AstNode node)
        {
            switch (node)
            {
                case SelectNode select:
                    return PlanSelect(select);

                case InsertNode:
                    return "PLAN: INSERT → Append to table (heap file)";

                case DeleteNode delete:
                    return PlanDelete(delete);

                default:
                    return "PLAN: UNKNOWN (No planner rule found)";
            }
        }

        // ---------------------------------------------------------------------
        // SELECT PLANNING
        // ---------------------------------------------------------------------
        private string PlanSelect(SelectNode node)
        {
            // No WHERE clause → full scan is the only option.
            if (string.IsNullOrWhiteSpace(node.Column) ||
                string.IsNullOrWhiteSpace(node.Value))
            {
                return "PLAN: SELECT → FULL_SCAN (no filter)";
            }

            // INDEX DETECTION PLACEHOLDER (Phase 3 of engine)
            // Here is where we will check if column has index file (.idx)
            bool indexExists = false; // ← Temporary, until storage is built

            if (indexExists)
                return $"PLAN: SELECT → USE_INDEX({node.Column})";

            return $"PLAN: SELECT → FULL_SCAN (no index found)";
        }

        // ---------------------------------------------------------------------
        // DELETE PLANNING
        // ---------------------------------------------------------------------
        private string PlanDelete(DeleteNode node)
        {
            // Same logic as SELECT for now — scan and filter
            return $"PLAN: DELETE → FULL_SCAN WHERE {node.Column} = {node.Value}";
        }
    }
}
