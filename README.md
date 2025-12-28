# RaptorDB

this is version1.1 with basic needs

🦖 RaptorDBA Commercial-Grade, Custom Database Engine built from scratch in C# > Locked and Loaded. 2025-2026 © Prayas (@captainprice27)RaptorDB is a lightweight, relational database management system (RDBMS) designed to demonstrate advanced storage engine concepts. It features a custom SQL parser, a B+ Tree indexing engine, and a robust REPL shell, all built without external database dependencies.🚀 Key Features🧠 Core EngineCustom Recursive Descent Parser: Supports standard SQL and custom syntax extensions.B+ Tree Indexing: Implements a disk-based B+ Tree for Primary Keys (4KB Paging) for O(log n) lookups.Typed Execution Engine: Strictly enforces data types (INT, LONG, FLOAT, STR, DATE, DATETIME).Portable Storage: Automatically adapts storage paths (Local Dev vs. Cloud/Azure).🔍 Advanced Querying (New!)Range Queries: Full support for >, <, >=, <=, and !=.Logic Chaining: Support for multiple AND conditions.Shorthand Syntax: unique syntax support like age > 18 AND < 25.BETWEEN Support: Syntactic sugar for range lookups.🛡️ Data IntegrityBase64 Serialization: Row data is Base64 encoded to prevent "Delimiter Injection" attacks.WAL (Write-Ahead Log): Logs all modification operations for auditability.Safe Mode: Atomic-like write operations (updates Index and Data separately).🛠️ ArchitectureRaptorDB follows a clean "Separation of Concerns" architecture:Code snippetgraph TD
    User[User Input] --> Lexer
    Lexer -->|Tokens| Parser
    Parser -->|AST| QueryPlanner
    QueryPlanner -->|Plan| ExecutionEngine
    
    subgraph Storage Layer
        ExecutionEngine --> SchemaManager
        ExecutionEngine --> RecordManager
        ExecutionEngine --> IndexManager
    end
    
    subgraph File System
        SchemaManager --> .schema
        RecordManager --> .data
        IndexManager --> .bpt
    end
💻 Installation & SetupClone the Repository:Bashgit clone https://github.com/captainprice27/RaptorDB.git
Open in Visual Studio:Open RaptorDB.sln in Visual Studio 2022.Run:Set RaptorDB as the Startup Project and hit F5.Storage Location:Local: Data is stored in bin/Debug/net8.0/Databases/.Cloud: Set RAPTOR_DB_PATH environment variable to override location.📖 Command Reference1. Database ManagementSQLCREATE DATABASE school;   -- Creates a new DB folder
USE school;               -- Switches context
DROP DATABASE school;     -- Deletes DB (with confirmation)
CURRENT DATABASE;         -- Shows active DB
LIST TABLES;              -- Lists all tables in active DB
2. Table OperationsNote: RaptorDB requires exactly one pk (Primary Key).SQL-- Supported Types: INT, LONG, FLOAT, STR, DATE, DATETIME, BOOL
CREATE TABLE students (
    id INT pk, 
    name STR, 
    gpa FLOAT, 
    dob DATE
);

DROP TABLE students;      -- Deletes schema, data, and index
3. Inserting DataSQL-- Standard Insert
INSERT INTO students (id, name, gpa, dob) 
VALUES (101, "Aman Sharma", 3.8, "2002-08-15");

-- Auto-checks for Duplicate Primary Keys
4. Querying Data (The Power of RaptorDB)RaptorDB supports a rich set of filtering options.Basic Select:SQLSELECT * FROM students;
SELECT name, gpa FROM students;
Range Queries:SQLSELECT * FROM students WHERE gpa >= 3.5;
SELECT * FROM students WHERE dob > "2000-01-01";
Complex Logic (AND / BETWEEN):SQL-- Standard AND
SELECT * FROM students WHERE gpa > 3.0 AND gpa < 4.0;

-- BETWEEN Syntax
SELECT * FROM students WHERE id BETWEEN 100 AND 200;

-- Custom Shorthand (Reuse Column)
SELECT * FROM students WHERE gpa > 3.0 AND < 4.0; 
5. Updating & DeletingSQLUPDATE students SET gpa = 4.0 WHERE id = 101;
DELETE FROM students WHERE gpa < 2.0;
📂 File StructureExtensionPurposeFormat.schemaTable DefinitionPlain Text (col:type:PK).dataRow DataPipe-delimited Base64 (`id=MQ==.bptIndex (Int Keys)Binary B+ Tree (4096 byte pages).bpt64Index (Long Keys)Binary B+ Tree (for Long/Date keys)wal.logAudit LogText Append-only🔮 Future ScopeRaptorDB is evolving. The following features are planned for v2.0:JOIN Support: Implementing Nested Loop Join to combine multiple tables.ACID Transactions: Full BEGIN TRANSACTION, COMMIT, ROLLBACK support using the WAL.Secondary Indexes: Allow indexing on non-PK columns (e.g., CREATE INDEX ON students(name)).Query Optimizer: Use the QueryPlanner to choose between Index Seek vs. Full Scan based on cost.Network Mode: Expose the engine via a TCP/REST API for remote access.🤝 ContributingContributions are welcome! Please fork the repository and create a Pull Request for review.Fork the ProjectCreate your Feature Branch (git checkout -b feature/NewFeature)Commit your Changes (git commit -m 'Add some NewFeature')Push to the Branch (git push origin feature/NewFeature)Open a Pull RequestBuilt with 🦖 & C# by Prayas