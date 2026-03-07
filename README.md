# 🦖 RaptorDB

> **Version 1.3** : A educational custom relational database engine built from scratch in C#.  
> Locked and Loaded just like a F22 raptor🦖. © 2025–2026 Prayas ([@captainprice27](https://github.com/captainprice27))

RaptorDB is a lightweight **Relational Database Management System (RDBMS)** designed to demonstrate advanced storage engine concepts. It features a custom SQL parser, a disk-based B+ Tree indexing engine, and an interactive REPL shell — all built **without any external database dependencies**.

---

## 📋 Table of Contents

- [Key Features](#-key-features)
- [Requirements](#-requirements)
- [Installation & Setup](#-installation--setup)
- [How to Run](#-how-to-run)
- [Data Types](#-data-types)
- [Command Reference](#-command-reference)
  - [Session Commands](#1-session-commands)
  - [Database Management](#2-database-management)
  - [Table Operations](#3-table-operations)
  - [Inserting Data](#4-inserting-data)
  - [Querying Data (SELECT)](#5-querying-data)
  - [Updating Data](#6-updating-data)
  - [Deleting Data](#7-deleting-data)
- [Architecture](#-architecture)
- [File Formats](#-file-formats)
- [Storage & Environment Config](#-storage--environment-config)
- [Future Scope (v2.0)](#-future-scope-v20)
- [Contributing](#-contributing)

---

## 🚀 Key Features

### 🧠 Core Engine
- **Custom Recursive Descent Parser** — Supports standard SQL syntax plus unique shorthand extensions
- **B+ Tree Indexing** — Disk-based B+ Tree for Primary Keys (4 KB paging) for **O(log n)** lookups
- **Typed Execution Engine** — Strictly enforces data types on every insert and update
- **Portable Storage** — Automatically adapts storage paths (local dev vs. cloud/Azure via env variable)

### 🔍 Advanced Querying
- **Range Queries** — Full support for `>`, `<`, `>=`, `<=`, and `!=` operators
- **Logic Chaining** — Chain multiple `AND` conditions in a single `WHERE` clause
- **Shorthand Syntax** — Unique syntax like `age > 18 AND < 25` (reuses column name)
- **BETWEEN Support** — Syntactic sugar for range lookups (`BETWEEN x AND y`)

### 🛡️ Data Integrity
- **Base64 Serialization** — All row data is Base64-encoded per field to prevent delimiter injection attacks
- **WAL (Write-Ahead Log)** — Every modification is logged to `wal.log` for full auditability
- **Duplicate PK Guard** — Automatically rejects inserts with duplicate primary keys
- **Safe Mode Writes** — Index and data files are updated separately to prevent partial-write corruption

---

## 💻 Requirements

| Requirement | Details |
|---|---|
| **IDE** | Visual Studio 2022+ (including VS 2026) |
| **Runtime** | .NET 8.0 or .NET 10.0 |
| **OS** | Windows (primary), Linux/macOS (via dotnet CLI) |
| **Dependencies** | `System.Configuration.ConfigurationManager` (NuGet — auto-restored) |

---

## 📦 Installation & Setup

**1. Clone the Repository**
```bash
git clone https://github.com/captainprice27/RaptorDB.git
```

**2. Open in Visual Studio**
- Open `RaptorDB.slnx` in Visual Studio 2022 or VS 2026

**3. Restore NuGet Packages**
- Visual Studio does this automatically on first build. Or run:
  ```bash
  dotnet restore
  ```

**4. Target Framework** (optional — supports both)

Edit `RaptorDB.csproj` to target a specific runtime or both:
```xml
<!-- .NET 8 only -->
<TargetFramework>net8.0</TargetFramework>

<!-- .NET 10 only -->
<TargetFramework>net10.0</TargetFramework>

<!-- Both (multi-targeting) -->
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

---

## ▶ How to Run

### Option A — Visual Studio (Recommended)
1. In **Solution Explorer**, right-click the `RaptorDB` project
2. Click **Set as Startup Project**
3. Press **F5** (Debug) or **Ctrl+F5** (Run without debugger)
4. A console window opens with the RaptorDB REPL shell

### Option B — dotnet CLI
```bash
cd path/to/RaptorDB
dotnet run
```

### What You'll See on Startup
```
[Storage] Data located at: C:\...\bin\Debug\net8.0\Databases
RaptorDB Locked and Loaded >> 🦖🦖🦖
© Prayas@captainprice27 2025-2026
Type 'exit'/'quit' to quit.

RaptorDB [default_db]>
```

The prompt dynamically shows the **currently active database** in brackets.

---

## 🗂️ Data Types

RaptorDB enforces strict typing. Use these types when defining columns:

| Type | Description | Example Value | Allowed as PK? |
|---|---|---|---|
| `INT` | 32-bit integer | `42`, `-5` | ✅ Yes |
| `LONG` | 64-bit integer | `9876543210` | ✅ Yes |
| `FLOAT` | Floating-point number | `3.14`, `99.9` | ❌ No |
| `STR` | Variable-length string | `"Alice"`, `"hello"` | ❌ No |
| `DATE` | Calendar date | `"2002-08-15"` | ✅ Yes |
| `DATETIME` | Date + time with ms | `"2024-01-01 10:30:00.00"` | ✅ Yes |

> **Note:** `BOOL` is listed in the README spec but is not yet fully implemented in `Validators.cs`. Use `INT` (0/1) as a workaround.

---

## 📖 Command Reference

> **General Rules:**
> - Commands are **case-insensitive** (`SELECT`, `select`, and `Select` all work)
> - Semicolons (`;`) are **optional** — the lexer strips them automatically
> - String values should be wrapped in **double or single quotes** (`"Alice"` or `'Alice'`)
> - Each table **must have exactly one** Primary Key column, declared with `pk`

---

### 1. Session Commands

These control the REPL shell itself.

| Command | Description |
|---|---|
| `exit` | Gracefully exit the RaptorDB shell |
| `quit` | Alias for `exit` |

```sql
exit
quit
```

---

### 2. Database Management

Databases are stored as **folders** inside the `Databases/` root directory.

#### `CREATE DATABASE`
Creates a new database (folder).
```sql
CREATE DATABASE school;
CREATE DATABASE company;
```

#### `USE`
Switches the active database context. All subsequent table operations apply to this DB.
```sql
USE school;
USE company;
```
The prompt updates to reflect the active DB: `RaptorDB [school]>`

#### `DROP DATABASE`
Permanently deletes a database and **all its tables**. A confirmation prompt appears.
```sql
DROP DATABASE school;
-- Prompt: "Are you sure you want to drop this database? (yes/no):"
```
> ⚠️ You **cannot** drop the currently active database. Switch to another DB first.

#### `CURRENT DATABASE`
Shows which database is currently active.
```sql
CURRENT DATABASE;
-- Output: Active Database: school
```

#### `LIST TABLES`
Lists all tables inside the currently active database.
```sql
LIST TABLES;
-- Output:
-- Tables in 'school':
--  📄 students
--  📄 courses
```

---

### 3. Table Operations

#### `CREATE TABLE`
Creates a new table with a defined schema. **Exactly one column must be marked as `pk`** (Primary Key).

**Syntax:**
```sql
CREATE TABLE <table_name> (
    <column_name> <TYPE> [pk],
    <column_name> <TYPE>,
    ...
);
```

**Examples:**
```sql
-- Simple student table
CREATE TABLE students (
    id   INT   pk,
    name STR,
    gpa  FLOAT,
    dob  DATE
);

-- Employee table with LONG pk
CREATE TABLE employees (
    emp_id   LONG  pk,
    name     STR,
    salary   FLOAT,
    joined   DATE
);

-- Event log with DATETIME
CREATE TABLE events (
    event_id  INT       pk,
    label     STR,
    occurred  DATETIME
);
```

> **Rules:**
> - Column names are lowercased internally (case-insensitive)
> - `pk` must be of type `INT`, `LONG`, `DATE`, or `DATETIME`
> - You cannot use `FLOAT` or `STR` as a primary key

#### `DROP TABLE`
Deletes the table schema, all its data, and its index files. A confirmation prompt appears.
```sql
DROP TABLE students;
-- Prompt: "Are you sure? (yes/no):"
```

---

### 4. Inserting Data

#### `INSERT INTO`
Inserts a single row into a table. Columns must match the schema, and types are validated.

**Syntax:**
```sql
INSERT INTO <table_name> (<col1>, <col2>, ...) VALUES (<val1>, <val2>, ...);
```

**Examples:**
```sql
-- Insert a student
INSERT INTO students (id, name, gpa, dob)
VALUES (101, "Aman Sharma", 3.8, "2002-08-15");

-- Insert with single quotes (also valid)
INSERT INTO students (id, name, gpa, dob)
VALUES (102, 'Priya Verma', 3.5, '2001-03-22');

-- Insert with LONG pk
INSERT INTO employees (emp_id, name, salary, joined)
VALUES (9876543210, "Rahul Kumar", 75000.50, "2023-06-01");
```

> **Duplicate PK Protection:** If you try to insert a row with an existing primary key value, the engine rejects it with:
> ```
> [Error] Duplicate primary key '101'.
> ```

---

### 5. Querying Data

RaptorDB supports rich `SELECT` queries with multiple filter types.

**Base Syntax:**
```sql
SELECT * FROM <table>;
SELECT <col1>, <col2> FROM <table> [WHERE <conditions>];
```

#### Basic SELECT (all rows)
```sql
-- All columns, all rows
SELECT * FROM students;

-- Specific column (note: multi-column select is a known v1.1 limitation)
SELECT name FROM students;
```

#### SELECT with WHERE (Equality)
```sql
SELECT * FROM students WHERE id = 101;
SELECT * FROM students WHERE name = "Alice";
```

#### Range Queries (`>`, `<`, `>=`, `<=`, `!=`)
Works on `INT`, `LONG`, `FLOAT`, `DATE`, and `DATETIME` columns.
```sql
-- Students with GPA 3.5 or above
SELECT * FROM students WHERE gpa >= 3.5;

-- Students born after year 2000
SELECT * FROM students WHERE dob > "2000-01-01";

-- Employees with salary not equal to 50000
SELECT * FROM employees WHERE salary != 50000;

-- GPA strictly less than 3.0
SELECT * FROM students WHERE gpa < 3.0;
```

#### BETWEEN (Range Sugar)
`BETWEEN x AND y` is equivalent to `>= x AND <= y`.
```sql
-- Students whose ID is between 100 and 200 (inclusive)
SELECT * FROM students WHERE id BETWEEN 100 AND 200;

-- GPA between 3.0 and 3.9
SELECT * FROM students WHERE gpa BETWEEN 3.0 AND 3.9;
```

#### Multiple AND Conditions
Chain multiple conditions using `AND`.
```sql
-- GPA > 3.0 AND GPA < 4.0 (standard form)
SELECT * FROM students WHERE gpa > 3.0 AND gpa < 4.0;

-- Two different columns
SELECT * FROM employees WHERE salary > 50000 AND joined > "2022-01-01";
```

#### Shorthand AND (Reuse Column Name)
RaptorDB's unique shorthand — omit the column name on the second condition when it's the same column.
```sql
-- Equivalent to: WHERE gpa > 3.0 AND gpa < 4.0
SELECT * FROM students WHERE gpa > 3.0 AND < 4.0;

-- Works with integers too
SELECT * FROM students WHERE id > 100 AND < 200;
```

---

### 6. Updating Data

#### `UPDATE ... SET ... WHERE`
Updates one or more rows that match the `WHERE` condition. Type validation is enforced on the new value.

**Syntax:**
```sql
UPDATE <table> SET <column> = <new_value> WHERE <condition>;
```

**Examples:**
```sql
-- Update GPA for a specific student
UPDATE students SET gpa = 4.0 WHERE id = 101;

-- Change a name
UPDATE students SET name = "Alice Smith" WHERE id = 101;

-- Adjust salary for all employees in a range
UPDATE employees SET salary = 80000 WHERE emp_id = 9876543210;
```

> **Note:** Currently `UPDATE` supports a single `SET` clause and a single `WHERE` condition. Multi-column updates are planned for v2.0.

---

### 7. Deleting Data

#### `DELETE FROM ... WHERE`
Deletes all rows that match the `WHERE` condition. If no `WHERE` is provided, **all rows are deleted** (use with caution).

**Syntax:**
```sql
DELETE FROM <table> WHERE <condition>;
DELETE FROM <table>;   -- Deletes ALL rows!
```

**Examples:**
```sql
-- Delete a specific student by PK
DELETE FROM students WHERE id = 101;

-- Delete all students with GPA below 2.0
DELETE FROM students WHERE gpa < 2.0;

-- Delete employees who joined before 2020
DELETE FROM employees WHERE joined < "2020-01-01";

-- WARNING: Deletes every row in the table
DELETE FROM students;
```

---

## 🏗️ Architecture

RaptorDB follows a clean **Separation of Concerns** pipeline:

```
User Input (REPL)
     │
     ▼
  Lexer                (Parser/Lexer.cs)
  Tokenizes raw SQL string → List<string> tokens
     │
     ▼
  Parser               (Parser/Parser.cs)
  Builds Abstract Syntax Tree (AST) from tokens
     │
     ▼
  AST Nodes            (Parser/AST/*.cs)
  Typed representation of each SQL statement
     │
     ▼
  ExecutionEngine      (Core/ExecutionEngine.cs)
  Dispatches AST nodes to storage layer
     │
     ├── SchemaManager  (Storage/SchemaManager.cs)  → reads/writes .schema files
     ├── RecordManager  (Storage/RecordManager.cs)  → reads/writes .data files
     ├── IndexManager   (Storage/IndexManager.cs)   → manages .bpt B+ Tree index
     └── WALManager     (Storage/WALManager.cs)     → appends to wal.log
```

### Key Design Decisions

| Decision | Rationale |
|---|---|
| Recursive Descent Parser | Flexible, human-readable, easy to extend with new syntax |
| B+ Tree on PK only | O(log n) duplicate-key detection at INSERT time |
| Base64 per-field encoding | Prevents `\|` or `=` inside data values from corrupting the row format |
| WAL as append-only text | Simple, auditable, human-readable audit trail |
| Env variable for path | Allows zero-code-change deployment to Azure/cloud |

---

## 📂 File Formats

All data is stored as **plain files** in the active database folder (`Databases/<db_name>/`).

| Extension | File Type | Format / Notes |
|---|---|---|
| `.schema` | Table definition | One line per column: `colname:TYPE:PK` (PK is empty string if not PK) |
| `.data` | Row storage | One row per line; pipe-delimited `col=Base64Value` pairs |
| `.bpt` | INT primary key index | Binary B+ Tree, 4096-byte pages |
| `.bpt64` | LONG/DATE/DATETIME PK index | Binary B+ Tree, 64-bit key variant |
| `wal.log` | Write-Ahead Log | Append-only text: `timestamp\|ACTION\|table\|details` |

**Example `.schema` file** for a `students` table:
```
id:INT:PK
name:STR:
gpa:FLOAT:
dob:DATE:
```

**Example `wal.log` entries:**
```
2025-12-27T14:30:01|INSERT|students|101
2025-12-27T14:30:45|UPDATE|students|gpa=4.0
2025-12-27T14:31:10|DELETE|students|Removed 2 rows
```

---

## ⚙️ Storage & Environment Config

### Default Storage Path

When running locally (debug or `dotnet run`), data is stored at:
```
<project_root>/bin/Debug/net8.0/Databases/
```
A `default_db` folder is automatically created here on first startup.

### Override Path (Cloud / Azure / Custom)

Set the `RAPTOR_DB_PATH` environment variable to redirect all storage:

**PowerShell (current session):**
```powershell
$env:RAPTOR_DB_PATH = "C:\MyDatabases\RaptorDB"
dotnet run
```

**System-wide (Windows):**
```text
Control Panel → System → Advanced system settings
→ Environment Variables → New → RAPTOR_DB_PATH = <your path>
```

When set, the engine prints:
```
[Storage] Data located at: C:\MyDatabases\RaptorDB
```

---

## 🔮 Future Scope (v2.x)

| Feature | Description | Status |
|---|---|---|
| **JOIN Support** | `INNER JOIN` using Nested Loop algorithm | 🔵 Planned |
| **ACID Transactions** | `BEGIN`, `COMMIT`, `ROLLBACK` backed by the existing WAL | 🔵 Planned |
| **Secondary Indexes** | `CREATE INDEX ON table(col)` for non-PK columns | 🔵 Planned |
| **Query Optimizer** | Wire up `QueryPlanner.cs` to choose Index Seek vs. Full Scan | 🔵 Planned |
| **`ORDER BY`** | Sort result set by any column, ASC or DESC | 🔵 Planned |
| **`LIMIT`** | Restrict query result to top N rows | 🔵 Planned |
| **`OR` Conditions** | Support `WHERE col = x OR col = y` | 🔵 Planned |
| **Multi-column `UPDATE`** | `UPDATE t SET a=1, b=2 WHERE ...` | 🔵 Planned |
| **Network / TCP Mode** | Expose engine via socket for remote or GUI clients | 🔵 Planned |
| **CSV Import/Export** | `EXPORT table TO "file.csv"` / `IMPORT INTO table FROM "file.csv"` | 🔵 Planned |
| **`LIST DATABASES`** | Show all available databases at the REPL | 🔵 Planned |
| **`HELP` Command** | Built-in command reference in the REPL | 🔵 Planned |

---

## 🤝 Contributing

Contributions are most welcome! Please please fork the repo and create a Pull Request for review.

```bash
# 1. Fork the project on GitHub

# 2. Create your Feature Branch
git checkout -b feature/NewFeature

# 3. Commit your Changes
git commit -m "Add: NewFeature description"

# 4. Push to the Branch
git push origin feature/NewFeature

# 5. Open a Pull Request on GitHub
```

---

<div align="center">

Built with 🦖 & C# by **Prayas** ([@captainprice27](https://github.com/captainprice27))

*"Small in size, Raptorous in speed."*

</div>