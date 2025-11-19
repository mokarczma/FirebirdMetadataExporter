# Firebird Metadata Exporter (.NET 8)

Aplikacja napisana jako zadanie rekrutacyjne.  
Służy do pracy na metadanych bazy Firebird 5.0 — generuje skrypty, tworzy nową bazę na ich podstawie oraz umożliwia aktualizację istniejącej bazy.

---

## Funkcjonalności

| Komenda | Opis |
|---------|------|
| `build-db` | Tworzy nową bazę Firebird 5.0 w podanym katalogu i wykonuje skrypty SQL. |
| `export-scripts` | Eksportuje metadane (domeny, tabele, procedury) z istniejącej bazy do plików `.sql`. |
| `update-db` | Wykonuje skrypty `.sql` na istniejącej bazie jako aktualizację struktury. |

---

## Obsługiwane obiekty

- domeny  
- tabele (z kolumnami)  
- procedury (w wersji uproszczonej — bez logiki wewnętrznej)

Ograniczenia zgodne ze specyfikacją zadania: brak obsługi triggerów, indeksów i constraintów.

---

## Przykłady użycia

### 1. Tworzenie nowej bazy na podstawie skryptów:

dotnet run -- build-db --db-dir "C:\db\clone" --scripts-dir "C:\scripts"


### 2. Eksport metadanych z istniejącej bazy:

dotnet run -- export-scripts --connection-string "User=SYSDBA;Password=masterkey;Database=C:\db\source.fdb;DataSource=localhost;Charset=UTF8" --output-dir "C:\out"


### 3. Aktualizacja istniejącej bazy:

dotnet run -- update-db --connection-string "User=SYSDBA;Password=masterkey;Database=C:\db\source.fdb;DataSource=localhost;Charset=UTF8" --scripts-dir "C:\scripts"


---

## Format wygenerowanych plików

Pliki eksportu są numerowane w kolejności odtwarzania:

01_domains.sql
02_tables.sql
03_procedures.sql


---

## Technologia

- .NET 8  
- FirebirdSql.Data.FirebirdClient  
- Firebird 5.0  
- Testy wykonane z użyciem IBExpert  

---

## Sposób realizacji

Projekt został zbudowany iteracyjnie z podziałem na:

- parsowanie i eksport metadanych Firebird z tabel systemowych (`rdb$*`),  
- wykonanie skryptów (z obsługą transakcji i kolejności),  
- test round-trip (baza → eksport → build-db → update-db).  

Kod został przygotowany tak, aby możliwe było debugowanie zarówno poprzez argumenty konsoli, jak i tryb interaktywny (`Console.ReadLine()`).

---

## Wykorzystanie AI

- generowanie przykładowych danych testowych (np. domeny, tabele, przykładowe procedury SQL użyte podczas testów)
- konsultacja zapytań do tabel systemowych Firebird (rdb$fields, rdb$relations, rdb$procedures) i mapowania typów danych
- inspiracja do struktury kodu pomocniczego (np. obsługa wykonywania batchowych skryptów SQL)
- usprawnienia formatowania i refaktoryzacji kodu C#


---

