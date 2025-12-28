using System;
using System.Collections.Generic;
using System.Linq;
using RaptorDB.RaptorDB.Parser.AST;

namespace RaptorDB.RaptorDB.Core
{
    /// <summary>
    /// The QueryPlanner is responsible for deciding *how* a query should execute.
    /// It acts as a decision layer between the Parser/AST and the ExecutionEngine.
    /// </summary>
    internal class QueryPlanner
    {
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
            // FIX: Check 'Conditions' list instead of old 'Column' property
            if (node.Conditions == null || node.Conditions.Count == 0)
            {
                return "PLAN: SELECT → FULL_SCAN (no filter)";
            }

            // Simple planner logic: Just list the filters
            string filters = string.Join(" AND ", node.Conditions.Select(c => $"{c.Column} {c.Operator} {c.Value}"));

            // INDEX DETECTION PLACEHOLDER (Phase 3)
            // Future logic: Check if any column in node.Conditions has an index
            bool indexExists = false;

            if (indexExists)
                return $"PLAN: SELECT → USE_INDEX ON FILTERS: {filters}";

            return $"PLAN: SELECT → FULL_SCAN WHERE {filters}";
        }

        // ---------------------------------------------------------------------
        // DELETE PLANNING
        // ---------------------------------------------------------------------
        private string PlanDelete(DeleteNode node)
        {
            // FIX: Check 'Conditions' list instead of old 'Column' property
            if (node.Conditions == null || node.Conditions.Count == 0)
            {
                return "PLAN: DELETE → FULL_SCAN (Dangerous! Deletes all rows)";
            }

            string filters = string.Join(" AND ", node.Conditions.Select(c => $"{c.Column} {c.Operator} {c.Value}"));
            return $"PLAN: DELETE → FULL_SCAN WHERE {filters}";
        }
    }
}