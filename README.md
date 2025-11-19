# FirebirdMetadataExporter

Recruitment task - tool for exporting and rebuilding Firebird 5.0 DB metadata (.NET 8).

The application is a simple console tool that can:
- build a new Firebird database from metadata scripts,
- export metadata (domains, tables, procedures) from an existing Firebird database,
- update an existing database based on scripts.

---

## Features

### `build-db`

Creates a new Firebird 5.0 database file and executes scripts from a given directory.

Example:

```powershell
DbMetaTool build-db --db-dir "D:\db\clone" --scripts-dir "D:\scripts"
