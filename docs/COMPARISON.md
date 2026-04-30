# 🦖 RaptorDB vs. MySQL / PostgreSQL

> A candid, side-by-side look at how **RaptorDB v2.0** compares to the two most popular production-grade open-source RDBMSs.
>
> RaptorDB is an **educational** engine built from scratch in C# / .NET 10. The goal of this document is not to claim parity, but to be honest about **what's similar, what's missing, and what's actually unique** — so contributors and learners know exactly where the engine stands.

---

## 📋 Table of Contents

- [TL;DR](#-tldr)
- [1. Where RaptorDB resembles MySQL / PostgreSQL](#-1-where-raptordb-resembles-mysql--postgresql)
- [2. Where RaptorDB does NOT catch up (v2.0 gaps)](#-2-where-raptordb-does-not-catch-up-v20-gaps)
- [3. Things RaptorDB has that MySQL / Postgres do NOT](#-3-things-raptordb-has-that-mysql--postgres-do-not)
- [4. Feature-Matrix Cheat Sheet](#-4-feature-matrix-cheat-sheet)
- [5. When to use which](#-5-when-to-use-which)

---

## 🚀 TL;DR

| Aspect | RaptorDB v2.0 | MySQL / PostgreSQL |
|---|---|---|
| **Purpose** | Teach how a database engine works internally | Run real production workloads |
| **Codebase size** | ~1.5 K lines, all C# | Millions of lines, C/C++ |
| **External deps** | Zero database deps | Many (kernel, networking, etc.) |
| **Storage** | Plain text + binary B+ Tree files | Tablespaces, page caches, WAL, MVCC |
| **Runs on** | Any .NET 10 host (Windows / Linux / macOS) | Same, plus countless OSes & cloud platforms |
| **Best for** | Learning parsers, B+ Trees, WAL, query exec | Anything serious 🙂 |

---

## ✅ 1. Where RaptorDB resembles MySQL / PostgreSQL

These are the **conceptual building blocks** that RaptorDB shares with the big engines — the same vocabulary, the same architecture layers, just simpler implementations.

### 1.1 SQL surface area
RaptorDB speaks recognizable SQL:

| Feature | RaptorDB | MySQL | PostgreSQL |
|---|:---:|:---:|:---:|
| `CREATE / DROP DATABASE` | ✅ | ✅ | ✅ |
| `USE <db>` (active context) | ✅ | ✅ | ⚠️ (uses `\c` in `psql`) |
| `CREATE / DROP TABLE` with typed columns | ✅ | ✅ | ✅ |
| `INSERT INTO ... VALUES (...)` | ✅ | ✅ | ✅ |
| `SELECT *` / single-column | ✅ | ✅ | ✅ |
| `WHERE` with `=`, `<`, `>`, `<=`, `>=`, `!=` | ✅ | ✅ | ✅ |
| `BETWEEN x AND y` | ✅ | ✅ | ✅ |
| Chained `AND` predicates | ✅ | ✅ | ✅ |
| `UPDATE ... SET ... WHERE` | ✅ | ✅ | ✅ |
| `DELETE FROM ... WHERE` | ✅ | ✅ | ✅ |
| `INNER / LEFT / RIGHT JOIN` (v2.0) | ✅ | ✅ | ✅ |
| **Chained / multi-table JOINs** (v2.0) | ✅ | ✅ | ✅ |
| Qualified `table.column` references | ✅ | ✅ | ✅ |
| `ORDER BY` with `ASC` / `DESC` + multi-key (v2.0) | ✅ | ✅ | ✅ |
| Case-insensitive keywords | ✅ | ✅ | ⚠️ (identifiers fold lowercase) |

### 1.2 Engine architecture
The pipeline mirrors a classic textbook RDBMS:

```
SQL text → Lexer → Parser → AST → Execution → Storage
```

This is exactly how MySQL and PostgreSQL are organised internally — they just have **vastly more sophisticated** versions of each layer (cost-based optimizers, MVCC snapshots, executor operators, buffer pools, etc.).

### 1.3 Storage primitives
RaptorDB uses the same *kinds* of artifacts the production engines use:

| Concept | RaptorDB file | MySQL (InnoDB) | PostgreSQL |
|---|---|---|---|
| Schema metadata | `.schema` | data dictionary tables | `pg_catalog` |
| Row data | `.data` (text, pipe-delimited Base64) | `.ibd` (paged binary) | heap files (paged binary) |
| Primary-key index | `.bpt` / `.bpt64` (4 KB paged B+ Tree) | clustered B+ Tree | B-Tree (heap-organized) |
| Write-Ahead Log | `wal.log` (append-only text) | redo log (`ib_logfile`) | `pg_wal` |

Same **concepts**, different fidelity.

### 1.4 Type system
Strict typing with parsing & validation on every write — same philosophy as MySQL/PostgreSQL.

| RaptorDB | Closest MySQL | Closest PostgreSQL |
|---|---|---|
| `INT` | `INT` | `integer` |
| `LONG` | `BIGINT` | `bigint` |
| `FLOAT` | `FLOAT` / `DOUBLE` | `real` / `double precision` |
| `STR` | `VARCHAR` / `TEXT` | `text` |
| `DATE` | `DATE` | `date` |
| `DATETIME` | `DATETIME` | `timestamp` |
| `BOOL` (v1.3) | `TINYINT(1)` / `BOOLEAN` | `boolean` |

### 1.5 Data integrity guards
- ✅ **Duplicate-PK rejection** at insert time (via B+ Tree probe) — same guarantee MySQL/Postgres give for `PRIMARY KEY`.
- ✅ **WAL audit trail** — every `INSERT` / `UPDATE` / `DELETE` / `DROP TABLE` is logged.
- ✅ **Strict per-column type validation** before write — locale-invariant parsing (since v1.3) prevents culture bugs.
- ✅ **Base64 per-field encoding** to prevent delimiter injection in row files.

---

## ❌ 2. Where RaptorDB does NOT catch up (v2.0 gaps)

This is the **honest gap list**. If a feature is here, MySQL/PostgreSQL have it and RaptorDB does **not** — yet.

### 2.1 Missing SQL features

| Category | Missing in RaptorDB | MySQL / Postgres status |
|---|---|---|
| **Aggregation** | `COUNT`, `SUM`, `AVG`, `MIN`, `MAX`, `GROUP BY`, `HAVING` | ✅ Standard |
| **Paging** | `LIMIT`, `OFFSET` *(`ORDER BY` ✅ added in v2.0 — see [§ Sorting in README](../README.md#9-sorting-results--order-by-v20))* | ✅ Standard |
| **Boolean logic** | `OR`, `NOT`, parenthesised predicates | ✅ Standard |
| **Subqueries** | Scalar, correlated, `IN (SELECT ...)`, `EXISTS` | ✅ Standard |
| **CTEs / Window functions** | `WITH`, `ROW_NUMBER()`, `RANK()`, `LAG()` | ✅ Standard |
| **Multi-column updates** | `SET a=1, b=2` | ✅ Standard |
| **Multi-row INSERT** | `INSERT ... VALUES (..),(..),(..)` | ✅ Standard |
| **Pattern matching** | `LIKE`, `ILIKE`, regex | ✅ Standard |
| **String / math built-ins** | `UPPER`, `LENGTH`, `COALESCE`, `CAST`, `+`, `-`, `*`, `/` in expressions | ✅ Standard |
| **JOIN extras** | `FULL OUTER JOIN`, `CROSS JOIN`, `USING`, composite `ON ... AND ...` | ✅ Standard |
| **Constraints** | `NOT NULL`, `UNIQUE` (non-PK), `CHECK`, `FOREIGN KEY` | ✅ Standard |
| **Default values** | `DEFAULT <expr>`, `AUTO_INCREMENT` / `SERIAL` / `IDENTITY` | ✅ Standard |
| **Views & schemas** | `CREATE VIEW`, namespacing | ✅ Standard |
| **Stored procedures / triggers** | `CREATE PROCEDURE`, `CREATE TRIGGER` | ✅ Standard |

### 2.2 Missing engine features

| Engine capability | Status in RaptorDB v2.0 |
|---|---|
| **ACID transactions** (`BEGIN` / `COMMIT` / `ROLLBACK`) | ❌ Each statement is auto-committed; WAL exists but is not yet used for rollback/recovery |
| **Concurrency control** (locking, MVCC) | ❌ Single-process, single-thread; no isolation levels |
| **Crash recovery via WAL replay** | ❌ WAL is audit-only |
| **NULL values in storage** | ❌ Every column must have a typed value; `<NULL>` is an output-only placeholder for `LEFT`/`RIGHT` joins |
| **Secondary indexes** (`CREATE INDEX ON t(col)`) | ❌ Index lives only on the primary key |
| **Cost-based query optimizer** | ❌ Naïve Nested-Loop join; no statistics, no plan choice |
| **Buffer pool / page cache** | ❌ Reads go straight to the OS file cache |
| **Replication** (primary→replica, streaming) | ❌ |
| **Network protocol / wire mode** | ❌ Embedded REPL only — no TCP, no client driver |
| **Authentication, roles, GRANT/REVOKE** | ❌ |
| **Backup / restore tooling** (`mysqldump`, `pg_dump`) | ❌ |
| **`EXPLAIN` / query plan visualisation** | ❌ |
| **Encoding / collation choice** | ⚠️ Fixed UTF-8, ordinal-ignore-case string compare |
| **Row size & table size limits** | ⚠️ Bounded only by file system; no enforced quotas |

### 2.3 Performance gaps
RaptorDB intentionally trades performance for clarity:

- **Joins are full Nested-Loop** — `O(n × m)` per step. MySQL/Postgres pick between Hash, Sort-Merge, Nested-Loop using statistics.
- **No buffer pool** — every `SELECT` re-reads files from disk. The big engines keep hot pages in RAM.
- **Append-only `RewriteTable` for UPDATE/DELETE** — RaptorDB rewrites the entire `.data` file. Production engines update in-place at the page level.
- **Single-threaded executor** — no parallel scans or workers.

This is a feature, not a flaw, for an **educational** engine — but it's why RaptorDB is for learning, not workloads.

---

## ✨ 3. Things RaptorDB has that MySQL / Postgres do NOT

These are the genuinely **distinctive** corners of the design. Most are consequences of being a small, hackable, single-binary engine.

### 3.1 Shorthand `WHERE ... AND <op> ...` syntax 🎯
RaptorDB lets you reuse the previous column name when chaining range predicates:

```sql
-- RaptorDB shorthand
SELECT * FROM students WHERE gpa > 3.0 AND < 4.0;

-- Equivalent in MySQL / Postgres (no shorthand)
SELECT * FROM students WHERE gpa > 3.0 AND gpa < 4.0;
```

Neither MySQL nor PostgreSQL supports omitting the column on the second predicate — you'd need `BETWEEN` or repeat the column name. This makes RaptorDB ranges feel slightly more *functional-style*.

### 3.2 Single self-contained binary, zero infra
- One `.exe` (~few MB), no daemon, no config files, no port to open, no `systemd` unit, no `my.cnf` / `postgresql.conf`.
- Starts in milliseconds; data lives in a folder you can `zip` and email.
- MySQL/Postgres both require a long-running server process and client driver.

### 3.3 Human-readable on-disk format
- `.data` files are line-per-row, pipe-delimited, **Base64 per field**. You can `cat` a table and visually verify it.
- `wal.log` is plain text with timestamps — `tail -f` works as your audit log.
- MySQL and Postgres on-disk pages are opaque binary by design.

### 3.4 `RAPTOR_DB_PATH` for instant relocatability
A single environment variable redirects *all* storage to any path — local SSD, network share, Azure File Share, container volume, etc. — with **zero config-file edits**.

```powershell
$env:RAPTOR_DB_PATH = "D:\my-databases"
dotnet run
```

MySQL/Postgres need `datadir` / `data_directory` edits in config files plus a server restart, often with permission gymnastics.

### 3.5 Output-time `<NULL>` for LEFT/RIGHT joins, despite NULL-free storage
RaptorDB's storage doesn't allow NULLs at all (every column is type-validated on write). Yet `LEFT` / `RIGHT` joins still work because the engine emits a synthetic `<NULL>` placeholder *only in the projection* — never persisted. This is a small but unusual design: a strict storage layer with a relaxed projection layer.

### 3.6 Embeddable in any .NET app
RaptorDB is a **library + REPL**. You can import the assembly into a .NET app and call `DBEngine.Process("SELECT * FROM users")` directly — no driver, no connection string, no serialization round-trip.

There's no comparable mode for MySQL/Postgres (the closest analog is SQLite, which is a different engine entirely).

### 3.7 Hackable in an afternoon
Because the entire engine is ~1.5 K lines of idiomatic C#, every feature added is easy to grok:

- One file = Lexer
- One file = Parser
- One folder = AST nodes
- One file = ExecutionEngine
- One file per Storage manager

You can add a new SQL keyword, trace it through the lexer/parser/AST/executor, and have it running end-to-end in a single sitting. Doing the same in MySQL or Postgres requires understanding their internal subsystems first.

### 3.8 Built specifically for learning
The codebase deliberately surfaces things production engines hide:

- **Pipeline stages are 1:1 with named files** — `Lexer.cs`, `Parser.cs`, `ExecutionEngine.cs`, `BPlusTree.cs`, `WALManager.cs`. Everything maps to a textbook chapter.
- **No code-gen, no macros, no templates** — pure straight-line C#.
- **No async / no concurrency** — easier to reason about correctness without threading concerns.

---

## 📊 4. Feature-Matrix Cheat Sheet

| Feature | RaptorDB v2.0 | MySQL | PostgreSQL |
|---|:---:|:---:|:---:|
| **DML / Query** | | | |
| `SELECT * / col` | ✅ | ✅ | ✅ |
| Multi-column `SELECT a, b` | ✅ (joined) / ⚠️ (single-table) | ✅ | ✅ |
| `WHERE` equality / range / `BETWEEN` | ✅ | ✅ | ✅ |
| `AND` chaining | ✅ | ✅ | ✅ |
| `OR` / `NOT` / parens | ❌ | ✅ | ✅ |
| `LIKE` / pattern match | ❌ | ✅ | ✅ |
| `ORDER BY` (`ASC` / `DESC`, multi-key, stable sort) | ✅ | ✅ | ✅ |
| `LIMIT`, `OFFSET` | ❌ | ✅ | ✅ |
| `GROUP BY` / aggregates | ❌ | ✅ | ✅ |
| Subqueries / CTEs / Window fns | ❌ | ✅ | ✅ |
| **Joins** | | | |
| `INNER JOIN` | ✅ | ✅ | ✅ |
| `LEFT JOIN` | ✅ | ✅ | ✅ |
| `RIGHT JOIN` | ✅ | ✅ | ✅ |
| `FULL OUTER JOIN` | ❌ | ⚠️ (emulated) | ✅ |
| `CROSS JOIN` | ❌ | ✅ | ✅ |
| Chained / multi-table | ✅ | ✅ | ✅ |
| Composite `ON ... AND ...` | ❌ | ✅ | ✅ |
| Hash / Merge join algorithms | ❌ (Nested-Loop only) | ✅ | ✅ |
| **Schema** | | | |
| Typed columns | ✅ | ✅ | ✅ |
| `PRIMARY KEY` constraint | ✅ | ✅ | ✅ |
| `NOT NULL` / `UNIQUE` / `CHECK` / `FK` | ❌ | ✅ | ✅ |
| `DEFAULT` / `AUTO_INCREMENT` | ❌ | ✅ | ✅ |
| Views | ❌ | ✅ | ✅ |
| **Engine** | | | |
| ACID transactions | ❌ | ✅ | ✅ |
| MVCC / row-level locking | ❌ | ✅ | ✅ |
| Crash recovery via WAL | ❌ (WAL audit-only) | ✅ | ✅ |
| Buffer pool / page cache | ❌ | ✅ | ✅ |
| Cost-based optimizer | ❌ | ✅ | ✅ |
| Secondary indexes | ❌ | ✅ | ✅ |
| Replication | ❌ | ✅ | ✅ |
| Network protocol | ❌ | ✅ | ✅ |
| Auth / roles | ❌ | ✅ | ✅ |
| Backup tooling | ❌ | ✅ (`mysqldump`) | ✅ (`pg_dump`) |
| **Distinctive to RaptorDB** | | | |
| Shorthand `AND <op>` (column reuse) | ✅ | ❌ | ❌ |
| Single binary, zero daemon | ✅ | ❌ | ❌ |
| Human-readable `.data` files | ✅ | ❌ | ❌ |
| Single env-var relocation (`RAPTOR_DB_PATH`) | ✅ | ❌ | ❌ |
| Embeddable as a .NET library | ✅ | ❌ | ❌ |

Legend: ✅ supported · ⚠️ partial / workaround · ❌ not supported

---

## 🎯 5. When to use which

### Use RaptorDB when…
- You're **learning** how databases work (parsers, B+ Trees, WAL, query exec).
- You want to **demo** a full storage engine in an interview / classroom / workshop.
- You're prototyping a **DSL** that needs SQL-like syntax with zero infra setup.
- You're embedding a **toy data layer** into a .NET app and want it to be inspectable.

### Use MySQL or PostgreSQL when…
- You need **transactions, concurrency, replication, recovery**.
- You need an **optimizer** that picks join algorithms based on statistics.
- You need **constraints, triggers, views, stored procedures**.
- You need **client drivers, network access, auth, backups**.
- You're building **anything that runs in production**.

---

## 🦖 Closing thought

RaptorDB doesn't try to replace MySQL or PostgreSQL — those have hundreds of person-years of engineering behind them. What RaptorDB *does* try to do is **make the core ideas of those engines small enough to read in a weekend**. Every line in the `Parser`, `BPlusTree`, `WALManager`, and `ExecutionEngine` files maps to a concept that exists, in 1000× more elaborate form, inside the production engines.

If you want to **understand** how databases work, the gap list above is also a roadmap — every ❌ is an opportunity to learn one more piece of database internals by implementing it yourself. 🚀
