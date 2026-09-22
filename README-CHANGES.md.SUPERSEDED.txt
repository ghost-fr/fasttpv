# FastTPV — Production-Ready Rebuild

This folder is a corrected, fully-wired copy of your FastTPV project. Your original
`FastTPV` folder is untouched — nothing there was deleted or modified. Once you've
verified this builds, you can delete the old folder and rename this one.

## What was actually wrong with the original

The project looked complete in its docs but wasn't wired up:

- `SalesWindow` and `InventoryWindow` were static XAML mockups — hardcoded fake
  products, no data binding, no commands. Clicking anything did nothing.
- `App.axaml.cs` never constructed any services or connected them to the UI.
- `SalesWindowViewModel` and `InventoryWindowViewModel` didn't exist at all.
- A real bug in `ArticleService`/`SaleService`: after an `INSERT`, the code read
  back the *affected row count* instead of the new auto-increment ID, then used
  that as the entity's `Id`. For sales specifically, this meant every
  `SaleLineItems` row would have been written with `SaleId = 0` (orphaned) or a
  garbage sale ID.
- `appsettings.json.template` existed but nothing in the code ever read it.

## What's fixed / new here

- `DatabaseContext` now opens a fresh connection per call (safe for concurrent
  async use) and has a real `ExecuteInsertAsync` that reads `LAST_INSERT_ID()`.
- `DatabaseInitializer.EnsureSchemaAsync` creates all four tables on first run —
  you only need an empty `FastTPV` database, not a manual schema script.
- `AppSettingsLoader` actually reads `appsettings.json` (connection string, tax
  rate, currency, auto-migrate flag), with safe defaults if the file is missing.
- `App.axaml.cs` is a real composition root: builds the services once, hands them
  to the windows, tests the DB connection at startup, and logs (rather than
  crashes) if the DB is unreachable.
- `SalesWindowViewModel`: live product search/category filter, a working cart
  (add/increment/decrement/remove), tax/total calculation, per-payment-method
  checkout that persists the sale, decrements stock, and generates sequential
  ticket numbers (`TKT-000123`).
- `InventoryWindowViewModel`: searchable product grid with a real add/edit/delete
  form wired to the database.
- `Program.cs`: global exception logging (`AppDomain.UnhandledException`,
  `TaskScheduler.UnobservedTaskException`) and log flushing on exit.
- Checkout is guarded by an `IsBusy` flag so double-clicking "Cash" can't create
  two sales from one cart.

## Setup

1. Create an empty database: `CREATE DATABASE FastTPV;`
2. Copy `FastTPV.Desktop/appsettings.json.template` to
   `FastTPV.Desktop/appsettings.json` and fill in your real MySQL credentials.
   (`appsettings.json` is not committed/synced with real credentials — the
   template is the only file with a placeholder password.)
3. `dotnet restore`
4. `dotnet build`
5. `dotnet run --project FastTPV.Desktop`

The schema is created automatically on first launch (as long as
`Database.AutoMigrate` stays `true` in your appsettings.json).

## Known limitations — not yet production-ready

Be aware of these before treating this as done:

- **No login enforcement.** `appsettings.json` has `Security.RequireLogin: true`
  and fields like `MinPasswordLength`, but there is no login screen, no user
  table, and no auth check anywhere in the app. Anyone who launches the exe is
  fully authorized. If you need multi-user/cashier tracking or access control,
  this needs to be built.
- **No automated tests.** Nothing here has unit or integration tests.
- **Not compiled or run.** The sandbox this was written in has no .NET SDK and
  no NuGet access, so none of this — including the original unmodified files —
  has actually been built or executed. Run `dotnet build` yourself before
  relying on it, and expect to fix small things (a missing using directive, a
  binding typo) that only show up at compile time.
- **Plaintext DB password** in `appsettings.json` — fine for a single desktop
  install, but if this ever needs to run on shared/multi-user machines, move the
  credential to Windows Credential Manager or an environment variable instead.
- **No receipt printing**, despite `POS.PrinterName`/`ReceiptFormat` existing in
  config — checkout completes and shows a ticket number, but nothing sends
  anything to a printer.
- `Core/Data/ApplicationConfig.cs` from the original project was left out here
  since it was dead code (nothing ever loaded it) — `AppSettingsLoader.cs`
  replaces its purpose.
