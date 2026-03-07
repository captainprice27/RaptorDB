# .NET 8 → .NET 10 Migration Log
### RaptorDB — v1.3 — March 2026

This document records every change made when migrating RaptorDB from **.NET 8.0** to **.NET 10.0**.  
Format per entry: **File → What changed → Before → After → Why**.

---

## 1. `RaptorDB.csproj` — Target Framework

| | Value |
|---|---|
| **Before** | `net8.0` |
| **After** | `net10.0` |
| **Reason** | Compile and run against the .NET 10 runtime. |

**Before**
```xml
<TargetFramework>net8.0</TargetFramework>
```
**After**
```xml
<TargetFramework>net10.0</TargetFramework>
```

---

## 2. `RaptorDB.csproj` — NuGet Package Version

| | Value |
|---|---|
| **Before** | `System.Configuration.ConfigurationManager` `8.0.1` |
| **After** | `System.Configuration.ConfigurationManager` `10.0.0` |
| **Reason** | Align the package version with the .NET 10 runtime to avoid compatibility warnings. |

**Before**
```xml
<PackageReference Include="System.Configuration.ConfigurationManager" Version="8.0.1" />
```
**After**
```xml
<PackageReference Include="System.Configuration.ConfigurationManager" Version="10.0.0" />
```

---

## 3. `RaptorDB.csproj` — Language Version & Metadata

| | Value |
|---|---|
| **Before** | No `LangVersion`, no assembly metadata |
| **After** | `LangVersion=latest`, `AssemblyVersion`, `Description`, `Authors`, `Copyright` |
| **Reason** | Unlock the latest C# features (collection expressions, required members, etc.) and record authorship. |

**After** *(added)*
```xml
<LangVersion>latest</LangVersion>
<AssemblyVersion>1.3.0.0</AssemblyVersion>
<Description>RaptorDB — Lightweight custom RDBMS engine built in C# (.NET 10)</Description>
<Authors>Prayas (@captainprice27)</Authors>
<Copyright>© 2025-2026 Prayas</Copyright>
```

---

## 4. `Program.cs` — Top-Level Statements

| | Style |
|---|---|
| **Before** | `namespace` + `internal class Program` + `static void Main()` boilerplate |
| **After** | Top-level statements (C# 9+, idiomatic .NET 6+) |
| **Reason** | Reduces noise; the `Main` wrapper adds nothing meaningful for a console app. |

**Before**
```csharp
namespace RaptorDB.RaptorDB
{
    internal class Program
    {
        static void Main()
        {
            var engine = new DBEngine();
            var repl = new ReplShell(engine);
            repl.Start();
        }
    }
}
```
**After**
```csharp
using RaptorDB.RaptorDB.Core;
using RaptorDB.RaptorDB.REPL;

var engine = new DBEngine();
var repl = new ReplShell(engine);
repl.Start();
```

---

## 5. `BPlusTreeDiskManager.cs` — Removed Dead Code

| | Lines |
|---|---|
| **Before** | ~135 lines of the old implementation left as `// comments` at the top of the file |
| **After** | Removed entirely |
| **Reason** | Dead commented-out code adds confusion and maintenance burden with zero benefit. |

---

## 6. `BPlusTreeDiskManager.cs` — Root Page Header I/O

| | API |
|---|---|
| **Before** | `FileStream` + `Seek(0)` + `BinaryReader`/`BinaryWriter` |
| **After** | `RandomAccess.Read` / `RandomAccess.Write` + `stackalloc Span<byte>` |
| **Reason** | `RandomAccess` (.NET 6+) reads/writes at a given offset without a `Seek()` call; `stackalloc` avoids a heap `byte[]` allocation for the 8-byte header. |

**Before**
```csharp
public long LoadRootPageId()
{
    using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
    using var br = new BinaryReader(fs);
    return br.ReadInt64();
}
```
**After**
```csharp
public long LoadRootPageId()
{
    using var handle = File.OpenHandle(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    Span<byte> buf = stackalloc byte[8];
    RandomAccess.Read(handle, buf, fileOffset: 0);
    return BitConverter.ToInt64(buf);
}
```

---

## 7. `BPlusTreeDiskManager.cs` — Page Zero-Fill Allocation

| | API |
|---|---|
| **Before** | `fs.Write(new byte[PageSize], 0, PageSize)` — heap-allocates a 4 KB array every call |
| **After** | `Span<byte> zeros = stackalloc byte[PageSize]; fs.Write(zeros);` — stack-allocated |
| **Reason** | 4 KB is within the typical stack budget for a leaf-level operation; avoids GC pressure on frequent page allocations. |

**Before**
```csharp
fs.Write(new byte[PageSize], 0, PageSize);
```
**After**
```csharp
Span<byte> zeros = stackalloc byte[PageSize]; // zero-initialised by the CLR
fs.Write(zeros);
```

---

## 8. `BPlusTreeDiskManager.cs` — FileStream Options

| | Options |
|---|---|
| **Before** | No `FileOptions` passed to any `FileStream` constructor |
| **After** | `FileOptions.RandomAccess`, `FileOptions.WriteThrough`, `FileOptions.SequentialScan` added as appropriate |
| **Reason** | Correct OS I/O hints: `RandomAccess` disables read-ahead for seek-heavy index access; `WriteThrough` ensures durability without a separate `Flush()`. |

**Before**
```csharp
using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
```
**After**
```csharp
using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
                              FileShare.Read, PageSize, FileOptions.RandomAccess);
```

---

## 9. `ByteSerializer.cs` — Base64 Encode (Span path)

| | API |
|---|---|
| **Before** | `Convert.ToBase64String(_encoding.GetBytes(input))` — two heap allocations |
| **After** | `stackalloc Span<byte>` + `_encoding.GetBytes(input, buffer)` + `Convert.ToBase64String(buffer[..written])` |
| **Reason** | Single stack buffer avoids the intermediate `byte[]` heap allocation for strings ≤ 512 bytes. |

**Before**
```csharp
return Convert.ToBase64String(_encoding.GetBytes(input));
```
**After**
```csharp
Span<byte> buffer = maxBytes <= 512 ? stackalloc byte[maxBytes] : new byte[maxBytes];
int written = _encoding.GetBytes(input, buffer);
return Convert.ToBase64String(buffer[..written]);
```

---

## 10. `ByteSerializer.cs` — Base64 Decode (no-exception path)

| | API |
|---|---|
| **Before** | `Convert.FromBase64String(input)` inside a `try/catch` — exception used for control flow |
| **After** | `Convert.TryFromBase64String(input, decoded, out int bytesLen)` |
| **Reason** | `TryFromBase64String` (.NET 5+) returns `false` on invalid input — no exception overhead for the backward-compatibility fallback. |

**Before**
```csharp
try { return _encoding.GetString(Convert.FromBase64String(input)); }
catch { return input; }
```
**After**
```csharp
if (Convert.TryFromBase64String(input, decoded, out int bytesLen))
    return _encoding.GetString(decoded[..bytesLen]);
return input; // not Base64 — backward-compat fallback
```

---

## 11. `ByteSerializer.cs` — Row Serialization (StringBuilder)

| | API |
|---|---|
| **Before** | `string.Join("|", row.Select(kv => $"..."))` — LINQ + intermediate array |
| **After** | Pre-sized `StringBuilder` with explicit loop |
| **Reason** | Avoids the LINQ `Select` intermediary allocation; `StringBuilder` pre-allocation reduces reallocations for wide rows. |

**Before**
```csharp
return string.Join("|", row.Select(kv => $"{kv.Key}={SafeEncode(kv.Value)}"));
```
**After**
```csharp
var sb = new StringBuilder(row.Count * 32);
bool first = true;
foreach (var (key, value) in row)
{
    if (!first) sb.Append('|');
    sb.Append(key).Append('=').Append(SafeEncode(value));
    first = false;
}
return sb.ToString();
```

---

## 12. `ByteSerializer.cs` — Row Deserialization Split

| | API |
|---|---|
| **Before** | `field.Split(new[] { '=' }, 2)` — allocates a `char[]` on every call |
| **After** | `field.Split('=', 2)` — .NET 5+ overload, no array allocation |
| **Reason** | Small but repeated allocation; the two-argument `char` overload is cleaner and allocation-free. |

**Before**
```csharp
var parts = field.Split(new[] { '=' }, 2);
```
**After**
```csharp
var parts = field.Split('=', 2);
```

---

## 13. `Validators.cs` — Culture-Invariant Numeric Parsing

| | API |
|---|---|
| **Before** | `int.TryParse(value, out _)` — uses current thread culture |
| **After** | `int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)` |
| **Reason** | On systems where the locale uses `,` as a decimal separator, culture-sensitive parsing silently produces wrong results or false negatives. Invariant culture guarantees consistent behaviour everywhere. Applies to `INT`, `LONG`, and `FLOAT`. |

**Before**
```csharp
case DataType.INT:
    return int.TryParse(value, out _);
case DataType.FLOAT:
    return float.TryParse(value, out _);
```
**After**
```csharp
case DataType.INT:
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
case DataType.FLOAT:
    return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                           CultureInfo.InvariantCulture, out _);
```

---

## 14. `Validators.cs` — BOOL Type Fully Implemented

| | Status |
|---|---|
| **Before** | `BOOL` fell through to `default: return false` — always invalid |
| **After** | Accepts `true`, `false`, `1`, `0` (case-insensitive); stored as canonical `"true"`/`"false"` |
| **Reason** | `BOOL` was declared in the `DataType` enum but never handled — broken in .NET 8 build. |

**Before**
```csharp
default:
    return false;
```
**After**
```csharp
DataType.BOOL =>
    bool.TryParse(value, out _) || value == "0" || value == "1",
```

---

## 15. `Validators.cs` — DATETIME: ISO 8601 with T-separator

| | Formats Accepted |
|---|---|
| **Before** | `"yyyy-MM-dd HH:mm:ss.ff"`, `"dd/MM/yyyy HH:mm:ss.ff"`, `"MM-dd-yyyy HH:mm:ss.ff"` |
| **After** | Added `"yyyy-MM-ddTHH:mm:ss"` and `"yyyy-MM-ddTHH:mm:ss.ff"` |
| **Reason** | .NET 10's `DateTime.ToString()` and `DateTimeOffset.ToString()` emit ISO 8601 with a `T` separator by default. Values generated by .NET 10 APIs would fail validation without these formats. |

**After** *(formats added)*
```csharp
"yyyy-MM-ddTHH:mm:ss",       // ISO 8601 — .NET 10 default output
"yyyy-MM-ddTHH:mm:ss.ff",    // ISO 8601 with hundredths
```

---

## 16. `Validators.cs` — Collection Expression Syntax

| | Syntax |
|---|---|
| **Before** | `new[] { "fmt1", "fmt2" }` |
| **After** | `["fmt1", "fmt2"]` |
| **Reason** | C# 12 collection expressions (enabled via `LangVersion=latest`) are more concise and the compiler may choose a more efficient representation. |

**Before**
```csharp
new[] { "yyyy-MM-dd HH:mm:ss.ff", "dd/MM/yyyy HH:mm:ss.ff" }
```
**After**
```csharp
["yyyy-MM-dd HH:mm:ss.ff", "dd/MM/yyyy HH:mm:ss.ff"]
```

---

## 17. `Parser.cs` — Nullable-Safe Token Consumption

| | Pattern |
|---|---|
| **Before** | `Pop()` returns `string` (was a lie — it returned `null` at end-of-stream); used directly as `string` locals without null checks |
| **After** | `Pop()` correctly returns `string?`; all required-token sites use new `Require(context)` helper which throws a clear parse error on `null` |
| **Reason** | .NET 10 / `<Nullable>enable</Nullable>` surfaces this as warnings. `Require()` also produces better error messages like `"expected table name"` instead of a `NullReferenceException`. |

**Before**
```csharp
private string Pop() => _pos < _tokens.Count ? _tokens[_pos++] : null;
// ...
string table = StripSemicolon(Pop()); // CS8600 warning
```
**After**
```csharp
private string? Pop() => _pos < _tokens.Count ? _tokens[_pos++] : null;

private string Require(string context)
{
    string? tok = Pop();
    if (tok is null) throw new Exception($"Syntax error: unexpected end of input (expected {context})");
    return tok;
}
// ...
string table = StripSemicolon(Require("table name")); // non-null guaranteed
```

---

## 18. `Parser.cs` — Multi-Column SELECT Bug Fixed

| | Behaviour |
|---|---|
| **Before** | `SELECT name, gpa FROM students` only returned column `name` — the comma-loop was missing |
| **After** | All listed columns are captured correctly |
| **Reason** | Discovered and fixed during the nullable-clean rewrite pass. |

**Before**
```csharp
else { cols.Add(StripSemicolon(Pop())); }
```
**After**
```csharp
else
{
    cols.Add(StripSemicolon(Require("column name")));
    while (Match(","))
        cols.Add(StripSemicolon(Require("column name")));
}
```

---

## 19. `Parser.cs` — Better End-of-Stream Error Messages

| | Message |
|---|---|
| **Before** | `$"Syntax error: expected '{token}', got '{Peek()}'"` → prints `null` literally |
| **After** | `$"Syntax error: expected '{token}', got '{Peek() ?? "<end>"}'"`|
| **Reason** | Showing `null` in an error message is confusing; `<end>` makes it clear the token stream was exhausted. |

---

## 20. `ColumnDefinition.cs` — `required` Property

| | Modifier |
|---|---|
| **Before** | `public string Name { get; set; }` — CS8618 warning (non-nullable unset in constructor) |
| **After** | `public required string Name { get; set; }` |
| **Reason** | `required` (C# 11 / .NET 7+) enforces that `Name` is always set at object initialisation, satisfying the nullable analyser without disabling it. |

---

## 21. `TableSchema.cs` — `required` + `[SetsRequiredMembers]`

| | Pattern |
|---|---|
| **Before** | `public string TableName { get; set; }` — CS8618 warning |
| **After** | `public required string TableName { get; set; }` with `[SetsRequiredMembers]` on both constructors |
| **Reason** | Same as above; `[SetsRequiredMembers]` tells the analyser that a constructor satisfies all required members. |

---

## 22. `SchemaManager.cs` / `RecordManager.cs` / `ExecutionEngine.cs` — `Normalize()` Contract

| | Pattern |
|---|---|
| **Before** | `private string Normalize(string name) => name?.Trim()...` — returned `string?` disguised as `string` |
| **After** | `private static string Normalize(string? name) { ArgumentNullException.ThrowIfNull(name); return name.Trim()...; }` |
| **Reason** | `ArgumentNullException.ThrowIfNull` (.NET 7+) is the canonical way to assert non-null at method entry. Returning a non-nullable `string` from a nullable-accepting parameter keeps all call-sites clean. |

**Before**
```csharp
private string Normalize(string name) => name?.Trim().TrimEnd(';').ToLower();
```
**After**
```csharp
private static string Normalize(string? name)
{
    ArgumentNullException.ThrowIfNull(name);
    return name.Trim().TrimEnd(';').ToLower();
}
```

---

## 23. `ConfigLoader.cs` — Null Guards & Typed Overloads

| | Change |
|---|---|
| **Before** | No input validation; only `string` return type |
| **After** | `ArgumentException.ThrowIfNullOrWhiteSpace` (.NET 7+) guards; added `GetBool()` and `GetInt()` typed overloads |
| **Reason** | Cleaner guard method replaces manual `if (string.IsNullOrWhiteSpace(...)) throw`; typed overloads prevent repetitive `bool.TryParse` at every call site. |

---

## Build Result

| Metric | .NET 8 build | .NET 10 build |
|---|---|---|
| Errors | 0 | **0** |
| Warnings | 0 (nullable off) | **0** (nullable on, all fixed) |
| Output | `net8.0/RaptorDB.dll` | `net10.0/RaptorDB.dll` |
| New APIs used | — | `RandomAccess`, `Span<byte>`, `stackalloc`, `Convert.TryFromBase64String`, `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, collection expressions `[...]`, `required` members, `[SetsRequiredMembers]` |

---

*Migration performed March 2026 on RaptorDB v1.3 branch.*  
*Built with 🦖 & C# by Prayas ([@captainprice27](https://github.com/captainprice27))*
