using FirebirdSql.Data.FirebirdClient;
using System.Text;

namespace DbMetaTool
{
    public static class Program
    {
        // Przykładowe wywołania:
        // DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
        // DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
        // DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Użycie:");
                Console.WriteLine("  build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
                Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
                Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                        {
                            string dbDir = GetArgValue(args, "--db-dir");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            BuildDatabase(dbDir, scriptsDir);
                            Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                            return 0;
                        }

                    case "export-scripts":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string outputDir = GetArgValue(args, "--output-dir");

                            ExportScripts(connStr, outputDir);
                            Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                            return 0;
                        }

                    case "update-db":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            UpdateDatabase(connStr, scriptsDir);
                            Console.WriteLine("Baza danych została zaktualizowana pomyślnie.");
                            return 0;
                        }

                    default:
                        Console.WriteLine($"Nieznane polecenie: {command}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
                return -1;
            }
        }

        private static string GetArgValue(string[] args, string name)
        {
            int idx = Array.IndexOf(args, name);
            if (idx == -1 || idx + 1 >= args.Length)
                throw new ArgumentException($"Brak wymaganego parametru {name}");
            return args[idx + 1];
        }

        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            if (string.IsNullOrWhiteSpace(databaseDirectory))
                throw new ArgumentException("Katalog bazy danych nie może być pusty.", nameof(databaseDirectory));
            if (string.IsNullOrWhiteSpace(scriptsDirectory))
                throw new ArgumentException("Katalog skryptów nie może być pusty.", nameof(scriptsDirectory));
            if (!Directory.Exists(scriptsDirectory))
                throw new DirectoryNotFoundException($"Nie znaleziono katalogu skryptów: {scriptsDirectory}");

            Directory.CreateDirectory(databaseDirectory);

            string dbPath = Path.Combine(databaseDirectory, "database.fdb");

            var csb = new FbConnectionStringBuilder
            {
                Database = dbPath,
                UserID = "SYSDBA",
                Password = "masterkey",
                DataSource = "localhost",
                Charset = "UTF8"
            };

            if (!File.Exists(dbPath))
            {
                FbConnection.CreateDatabase(csb.ToString(), 4096, true);
            }

            string[] scriptFiles = Directory
                .GetFiles(scriptsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f)
                .ToArray();

            if (scriptFiles.Length == 0)
            {
                Console.WriteLine("Brak plików .sql w katalogu skryptów.");
                return;
            }

            using var connection = new FbConnection(csb.ToString());
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var file in scriptFiles)
                {
                    Console.WriteLine($"Wykonywanie skryptu: {file}");
                    string sql = File.ReadAllText(file);

                    ExecuteSqlScript(connection, transaction, sql);
                }


                transaction.Commit();
                Console.WriteLine("Budowa bazy zakończona powodzeniem.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd podczas budowania bazy danych:");
                Console.WriteLine(ex.Message);
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string nie może być pusty.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Katalog wyjściowy nie może być pusty.", nameof(outputDirectory));

            Directory.CreateDirectory(outputDirectory);

            using var connection = new FbConnection(connectionString);
            connection.Open();

            // --- DOMENY ---
            var domainsSb = new StringBuilder();
            domainsSb.AppendLine("-- DOMAINS");

            using (var cmd = new FbCommand(@"
        SELECT TRIM(f.rdb$field_name) AS name,
               f.rdb$field_type,
               f.rdb$field_length
          FROM rdb$fields f
         WHERE (f.rdb$system_flag IS NULL OR f.rdb$system_flag = 0)
           AND f.rdb$field_name NOT LIKE 'RDB$%'
           AND COALESCE(f.rdb$computed_blr, '') = ''
    ", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    short fieldType = reader.GetInt16(1);
                    short length = reader.GetInt16(2);

                    string sqlType = MapFirebirdType(fieldType, length);
                    domainsSb.AppendLine($"CREATE DOMAIN {name} AS {sqlType};");
                }
            }

            string domainsPath = Path.Combine(outputDirectory, "01_domains.sql");
            File.WriteAllText(domainsPath, domainsSb.ToString(), Encoding.UTF8);

            // --- TABELLE ---
            var tablesSb = new StringBuilder();
            tablesSb.AppendLine("-- TABLES");

            // lista tabel użytkownika
            var tables = new List<string>();
            using (var tableCmd = new FbCommand(@"
        SELECT TRIM(rdb$relation_name)
          FROM rdb$relations
         WHERE rdb$system_flag = 0
           AND rdb$view_blr IS NULL
    ", connection))
            using (var tableReader = tableCmd.ExecuteReader())
            {
                while (tableReader.Read())
                    tables.Add(tableReader.GetString(0));
            }

            foreach (var table in tables)
            {
                tablesSb.AppendLine($"CREATE TABLE {table} (");

                using var colsCmd = new FbCommand(@"
            SELECT TRIM(rf.rdb$field_name) AS col_name,
                   f.rdb$field_type,
                   f.rdb$field_length
              FROM rdb$relation_fields rf
              JOIN rdb$fields f ON rf.rdb$field_source = f.rdb$field_name
             WHERE rf.rdb$relation_name = @tableName
             ORDER BY rf.rdb$field_position
        ", connection);

                colsCmd.Parameters.AddWithValue("tableName", table);

                bool first = true;
                using var colReader = colsCmd.ExecuteReader();
                while (colReader.Read())
                {
                    if (!first)
                        tablesSb.AppendLine(",");
                    first = false;

                    string colName = colReader.GetString(0);
                    short fieldType = colReader.GetInt16(1);
                    short length = colReader.GetInt16(2);
                    string sqlType = MapFirebirdType(fieldType, length);

                    tablesSb.Append($"    {colName} {sqlType}");
                }

                tablesSb.AppendLine();
                tablesSb.AppendLine(");");
                tablesSb.AppendLine();
            }

            string tablesPath = Path.Combine(outputDirectory, "02_tables.sql");
            File.WriteAllText(tablesPath, tablesSb.ToString(), Encoding.UTF8);

            // --- PROCEDURY (uproszczone) ---
            var procSb = new StringBuilder();
            procSb.AppendLine("-- PROCEDURES");

            using (var procCmd = new FbCommand(@"
        SELECT TRIM(rdb$procedure_name)
          FROM rdb$procedures
         WHERE rdb$system_flag = 0
    ", connection))
            using (var procReader = procCmd.ExecuteReader())
            {
                while (procReader.Read())
                {
                    string procName = procReader.GetString(0);

                    procSb.AppendLine($"CREATE OR ALTER PROCEDURE {procName}");
                    procSb.AppendLine("AS");
                    procSb.AppendLine("BEGIN");
                    procSb.AppendLine("    /* TODO: procedure body not exported in this version */");
                    procSb.AppendLine("END;");
                    procSb.AppendLine();
                }
            }

            string procPath = Path.Combine(outputDirectory, "03_procedures.sql");
            File.WriteAllText(procPath, procSb.ToString(), Encoding.UTF8);

            Console.WriteLine("Eksport metadanych zakończony. Wygenerowano pliki:");
            Console.WriteLine(domainsPath);
            Console.WriteLine(tablesPath);
            Console.WriteLine(procPath);
        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Wykonaj skrypty z katalogu scriptsDirectory (tylko obsługiwane elementy).
            // 3) Zadbaj o poprawną kolejność i bezpieczeństwo zmian.
            throw new NotImplementedException();
        }


        /// <summary>
        /// Proste wykonywanie skryptu SQL: dzieli na polecenia zakończone średnikiem.
        /// Nie obsługuje zagnieżdżonych średników w ciele procedur itp.,
        /// ale wystarcza dla wygenerowanych tutaj prostych CREATE TABLE/DOMAIN/PROCEDURE.
        /// </summary>
        private static void ExecuteSqlScript(FbConnection connection, FbTransaction transaction, string script)
        {
            using var reader = new StringReader(script);
            var sb = new StringBuilder();

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();

                // pomijamy komentarze i puste linie
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("--"))
                    continue;

                sb.AppendLine(line);

                // jeśli linia kończy się średnikiem, traktujemy to jako koniec polecenia
                if (trimmed.EndsWith(";"))
                {
                    var commandText = sb.ToString().Trim();

                    // usuwamy końcowe średniki, żeby nie bruździły
                    while (commandText.EndsWith(";"))
                        commandText = commandText[..^1].TrimEnd();

                    if (!string.IsNullOrWhiteSpace(commandText))
                    {
                        using var cmd = new FbCommand(commandText, connection, transaction);
                        cmd.ExecuteNonQuery();
                    }

                    sb.Clear();
                }
            }

            // Jeśli coś zostało bez końcowego średnika, też spróbuj wykonać
            var rest = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(rest))
            {
                using var cmd = new FbCommand(rest, connection, transaction);
                cmd.ExecuteNonQuery();
            }
        }

        private static string MapFirebirdType(short fieldType, short length)
        {
            return fieldType switch
            {
                7 => "SMALLINT",
                8 => "INTEGER",
                10 => "FLOAT",
                12 => "DATE",
                13 => "TIME",
                14 => $"CHAR({length})",
                16 => "BIGINT",
                27 => "DOUBLE PRECISION",
                35 => "TIMESTAMP",
                37 => $"VARCHAR({length})",
                _ => "BLOB"
            };
        }
    }
}
