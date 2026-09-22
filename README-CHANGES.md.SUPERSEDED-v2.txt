# FastTPV — Production-Ready Rebuild

This folder is a corrected, fully-wired copy of your FastTPV project. Your original
`FastTPV` folder is untouched. Once you've verified this builds, you can delete the
old folder and rename this one.

**A note on file replacement:** the Drive connector this was built through can't
delete or overwrite files in place, so a few files exist in two versions here —
the current one, and an old one renamed with a `.SUPERSEDED` suffix (e.g.
`App.axaml.cs` vs `App.axaml.cs.SUPERSEDED`). Only the file WITHOUT the
`.SUPERSEDED` suffix is current; delete the `.SUPERSEDED` ones once you've copied
this folder into your project — they're not part of the build (the extension
means .NET's build system ignores them).

## What was actually wrong with the original

- `SalesWindow` and `InventoryWindow` were static XAML mockups — hardcoded fake
  products, no data binding, no commands. Clicking anything did nothing.
- `App.axaml.cs` never constructed any services or connected them to the UI.
- `SalesWindowViewModel` and `InventoryWindowViewModel` didn't exist at all.
- A real bug in `ArticleService`/`SaleService`: after an `INSERT`, the code read
  back the *affected row count* instead of the new auto-increment ID. For sales
  specifically, this meant `SaleLineItems` rows would have been written with
  `SaleId = 0` or a garbage sale ID.
- `appsettings.json.template` existed but nothing in the code ever read it.
- Config declared `Security.RequireLogin: true` but there was no login screen,
  no user table, and no auth check anywhere.

## What's fixed / new here

**Core fixes (Sales + Inventory):**
- `DatabaseContext` opens a fresh connection per call and has a real
  `ExecuteInsertAsync` that reads `LAST_INSERT_ID()`.
- `DatabaseInitializer.EnsureSchemaAsync` creates all six tables on first run —
  Articles, Customers, Sales, SaleLineItems, Users, AuditLog.
- `AppSettingsLoader` reads the full set of settings the app now uses:
  connection string, tax rate, currency, auto-migrate, auto-logoff minutes,
  printer name/format, and the Security section.
- `SalesWindowViewModel`: search/filter, cart, checkout with sequential ticket
  numbers, stock decrement, receipt printing, and audit logging.
- `InventoryWindowViewModel`: searchable grid with add/edit/delete.
- `Program.cs`: global exception logging and log flushing.
- Checkout is guarded by `IsBusy` so double-clicking "Cash" can't double-charge.

**New this pass — Login, Customers, Reports, Receipts, Audit (`Features/` folder):**
- `AppRuntime` — a static composition root all the Features windows and
  MainWindowViewModel read services from. Built once in `App.axaml.cs`.
- `LoginWindow` — real sign-in, checked against the new `Users` table with
  PBKDF2 password hashing (`UserAccountService`). The app now starts here
  instead of at `MainWindow`.
- **First-run bootstrap:** if the `Users` table is empty, `AppRuntime` seeds a
  default admin account and logs the generated password once
  (username `admin`, password `ChangeMe123!`). **Sign in and change this
  immediately** — there's no in-app password-change screen yet (see gaps below).
- `SessionManager` — enforces `POS.AutoLogoffMinutes`: any pointer/keyboard
  activity on the main window resets the idle clock; a background check every
  30s force-logs-out an idle session back to the login screen.
- `CustomerWindow` — full add/edit/delete UI for customers (the backend already
  existed; this was the missing screen).
- `ReportsWindow` — sales-in-range totals/list plus a low-stock list, both
  reading through the existing `SaleService`/`ArticleService` via a thin new
  `ReportsService`.
- `ReceiptService` — writes a text receipt to
  `%LocalAppData%/FastTPV/Receipts/` on every completed sale, and on Windows
  additionally attempts to send it to the configured printer via the shell
  "print" verb (best-effort; wrapped in try/catch so a printer failure can't
  block or undo an already-completed sale). **This print step is Windows-only**
  — on Linux/macOS only the text file is written, printing is silently skipped.
- `AuditService` — logs Login, and Customer Create/Update/Delete, and Sale
  completion to the new `AuditLog` table with the acting user's ID.
- `MainWindow` now has Customers and Reports entries (sidebar + dashboard
  cards), a "Signed in as {name}" label, and a Log out button.

## Setup

1. Create an empty database: `CREATE DATABASE FastTPV;`
2. Copy `FastTPV.Desktop/appsettings.json.template` to
   `FastTPV.Desktop/appsettings.json` and fill in your real MySQL credentials.
3. `dotnet restore`
4. `dotnet build`
5. `dotnet run --project FastTPV.Desktop`
6. On first launch, sign in with `admin` / `ChangeMe123!` (check the log file
   under `Logs/` if you missed the console output) and change it immediately —
   see the password-change gap below.

The schema, including `Users` and `AuditLog`, is created automatically on first
launch as long as `Database.AutoMigrate` stays `true`.

## Known limitations — not yet production-ready

- **No password-change or user-management UI.** You can sign in and use the
  app, but creating additional cashier accounts or changing the admin password
  currently means writing directly to the `Users` table using the same
  PBKDF2 hash/salt shape `UserAccountService.HashPassword` produces. This is
  the single biggest remaining gap for real multi-user deployment.
- **No automated tests.**
- **Not compiled or run.** No .NET SDK or NuGet access in the sandbox this was
  built in — run `dotnet build` yourself and expect to fix small things (a
  missing using directive, a binding typo) that only surface at compile time.
  The Login → Main window handoff in particular (`ShutdownMode` change in
  `App.axaml.cs`) is exactly the kind of thing worth testing manually first —
  confirm the app doesn't quit when the login window closes after a
  successful sign-in.
- **Plaintext DB password** in `appsettings.json` — fine for one desktop
  install, not for shared/multi-user machines (use Windows Credential Manager
  or an environment variable instead).
- **Receipt printing is Windows-only** (see above) and is a naive text-file
  print, not a real POS receipt-printer driver/ESC-POS integration.
- **Session timeout is approximate**, checked every 30 seconds rather than
  exactly at the configured minute — acceptable for an idle-logout feature,
  just not to-the-second precise.
- `Core/Data/ApplicationConfig.cs` from the original project was left out
  since it was dead code — `AppSettingsLoader.cs`/`RuntimeSettings` replaces it.
