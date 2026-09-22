# FastTPV — Production-Ready Rebuild

This folder is a corrected, fully-wired copy of your FastTPV project. Your original
`FastTPV` folder is untouched. Once you've verified this builds, you can delete the
old folder and rename this one.

**A note on file replacement:** the Drive connector this was built through can't
delete or overwrite files in place, so many files exist in multiple versions — the
current one, and old ones renamed with a `.SUPERSEDED*` suffix. **Only the file
WITHOUT any such suffix is current.** Delete every `.SUPERSEDED*` file once you've
copied this into your project.

## Read this if you're picking this project back up

Multiple work passes touched this folder without always superseding each other's
files, which left several **duplicate-class compile errors** sitting in the folder
(two files both defining the same C# class) and a few files physically misplaced in
the wrong project subfolder. This pass found and fixed all of them:

- **`AppRuntime.cs`** existed twice in `Features/` (neither marked superseded) — kept
  the version with `SettingsPath`/`PersistPrinterSettings`/`PersistCompanySettings`.
- **`ReceiptService.cs`** existed twice in `Features/` — kept the version with
  company header/footer support (`GetReceiptHeaderLines`, `ReceiptFooter`).
- **`AppSettingsLoader.cs`** (defines `RuntimeSettings`) existed in `Core/Data/`
  (correct project) AND, separately, a newer/more complete copy had been placed in
  `Desktop/Features/` (wrong project — Core and Desktop are separate .csproj files,
  so the same class defined in both is a hard compile error, not just clutter).
  Moved the complete version into `Core/Data/` where it belongs.
- **`appsettings.json.template`** — same problem, same fix: consolidated into
  `Desktop/` (its correct location) with the full Company section.
- **`MainWindowViewModel.cs`** existed in `ViewModels/` (correct folder, missing the
  new Company/Printer Settings commands) AND a separate copy in `Features/` (wrong
  folder, had `OpenCompanySettingsCommand` but was still missing
  `OpenPrinterSettingsCommand` entirely — so PrinterSettingsWindow had no way to be
  opened from the UI at all). Merged into one correct file with both commands, in
  `ViewModels/` where it belongs, and added the missing Printer Settings nav item.
- **`MainWindow.axaml`** updated to add "Company Settings" and "Printer Settings" to
  the sidebar and dashboard (both Admin-only, matching Users).

Also worth flagging: an earlier pass fixed a real bug that had been in `App.axaml.cs`
since the very first rebuild — it used `IClassicDesktopApplicationLifetime`, which
**does not exist** in Avalonia (the real type is `IClassicDesktopStyleApplicationLifetime`,
with "Style"). That would have failed to compile at the application's entry point in
every version of this project delivered before that fix. It's fixed now, but it's a
good example of why "not compiled or run" (below) is the gap that matters most.

## What was actually wrong with the original FastTPV project

- `SalesWindow`/`InventoryWindow` were static XAML mockups with no data binding.
- `App.axaml.cs` never constructed any services or connected them to the UI.
- A bug in `ArticleService`/`SaleService`: after an `INSERT`, the code read back
  the *affected row count* instead of the new auto-increment ID.
- Config declared `Security.RequireLogin: true` but there was no login, no user
  table, no auth check anywhere.
- `POS.PrinterName`/`ReceiptFormat` existed but nothing printed anything.

## What's in this build now

- **Sales + Inventory**: real search/cart/checkout/CRUD, correct insert-ID handling,
  auto-schema creation, double-submit protection, barcode-scanner quick-add
  (exact-code match on Enter in the search box).
- **Login, session, audit**: `LoginWindow` with PBKDF2-hashed passwords, session
  idle-timeout enforcement, `AuditLog` of logins/sales/customer & user changes.
  First-run bootstrap seeds `admin` / `ChangeMe123!` if the `Users` table is empty.
- **User management & roles**: `UsersWindow` (Admin-only) for creating accounts,
  setting Admin/Cashier roles, activating/deactivating, and resetting passwords
  (blocks an admin from locking themselves out). `ChangePasswordWindow` for
  self-service password changes, enforcing `Security.MinPasswordLength`.
- **Customers**: full CRUD screen.
- **Reports**: sales-in-range + low-stock, with **Reprint** and **Void** (restocks
  the voided sale's items) per ticket.
- **Real ESC/POS receipt printing**: `EscPosBuilder` sends raw, standard ESC/POS
  bytes over a TCP socket (`NetworkPrinterClient`, port 9100 — the near-universal
  raw-printing port for network thermal printers) via `POS.PrinterConnection =
  "Network"`. Code page and cut-command style are configurable rather than
  hardcoded, since neither is reliably guessable from a printer's brand alone.
- **Printer Settings** (Admin-only): configure connection type, IP/port, paper
  width, code page, and cut style — with a **"Send test print"** button that prints
  a small ticket (including accented characters) straight to the hardware so you
  can verify the settings before saving, rather than guessing and running a real sale.
- **Company Settings** (Admin-only): business name, tax ID, address, contact info,
  and a receipt header/footer — printed on every receipt via
  `RuntimeSettings.GetReceiptHeaderLines()`.

## Setup

1. `CREATE DATABASE FastTPV;`
2. Copy `FastTPV.Desktop/appsettings.json.template` to
   `FastTPV.Desktop/appsettings.json`, fill in your real MySQL credentials.
3. `dotnet restore && dotnet build && dotnet run --project FastTPV.Desktop`
4. Sign in with `admin` / `ChangeMe123!`, then immediately use "Change password"
   (top bar).
5. If you have a network thermal printer: open **Printer Settings** (Admin), set
   connection to Network + your printer's IP, use **Send test print** to dial in
   the code page/cut style against the real hardware, then Save.
6. Open **Company Settings** (Admin) to put your real business details on receipts.

## Known limitations — not yet production-ready

- **Not compiled or run, still.** No .NET SDK/NuGet access in the sandbox this was
  built in, across every pass including this one. Given the duplicate-class and
  nonexistent-type bugs found and fixed in this pass alone, treat "should compile
  now" with real skepticism until you've actually run `dotnet build` yourself.
  After that, specifically exercise: sign-in → change password → sign-out → sign-in
  with the new password; creating a user in Users and signing in as them; a
  simulated barcode scan (type a product's exact Code into Sales' search box,
  press Enter); Reprint and Void from Reports; and, if you have a network printer,
  an actual test print from Printer Settings.
- **No automated tests.**
- **No user-facing "forgot password" recovery** — an Admin resets it via Users.
- **Plaintext DB password** in `appsettings.json` — fine for one desktop install,
  not for shared machines or source control.
- **Session timeout is approximate** (checked every 30s, not to-the-second).
- `Core/Data/ApplicationConfig.cs` from the original project was left out since
  it was dead code — `AppSettingsLoader.cs`/`RuntimeSettings` replaces it.

## Deferred — need your input before building, not just code gaps

- **Cash drawer reconciliation** (open with a float, close/count cash) and
  **discounts** (per-line vs per-ticket, manager approval or not) — need workflow
  decisions from you before building the right thing.
- **Automated tests, CI/CD, an installer** — need your infra choices (hosting,
  code-signing, auto-update or not).
- **Offline mode, multi-store support, Spanish fiscal compliance (VeriFactu/AEAT)**
  — each a substantial architectural project on its own.
