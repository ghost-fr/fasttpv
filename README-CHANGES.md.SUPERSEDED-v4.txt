# FastTPV — Production-Ready Rebuild

This folder is a corrected, fully-wired copy of your FastTPV project. Your original
`FastTPV` folder is untouched. Once you've verified this builds, you can delete the
old folder and rename this one.

**A note on file replacement:** the Drive connector this was built through can't
delete or overwrite files in place, so several files exist in two or more versions —
the current one, and old ones renamed with a `.SUPERSEDED` (or `.SUPERSEDED-v2`,
etc.) suffix. Only the file WITHOUT any such suffix is current. Delete the
`.SUPERSEDED*` ones once you've copied this into your project — the odd extension
means .NET's build system ignores them either way, but they're dead weight.

## What was actually wrong with the original

- `SalesWindow`/`InventoryWindow` were static XAML mockups with no data binding.
- `App.axaml.cs` never constructed any services or connected them to the UI.
- A bug in `ArticleService`/`SaleService`: after an `INSERT`, the code read back
  the *affected row count* instead of the new auto-increment ID — would have
  written `SaleLineItems` rows with `SaleId = 0`.
- `appsettings.json.template` existed but nothing ever read it.
- Config declared `Security.RequireLogin: true` but there was no login, no user
  table, no auth check anywhere.
- `POS.PrinterName`/`ReceiptFormat` existed but nothing printed anything.

## What's fixed / new here

**Core fixes (Sales + Inventory):** correct insert-ID handling, per-call DB
connections, auto-schema creation, real `SalesWindowViewModel`/
`InventoryWindowViewModel` with search/cart/checkout/CRUD, global exception
logging, double-submit protection on checkout.

**Login, Customers, Reports, Audit, Users (`Features/` folder):** `AppRuntime`
composition root; `LoginWindow` with PBKDF2-hashed passwords against a `Users`
table (first-run bootstrap seeds `admin` / `ChangeMe123!`); session idle-timeout
enforcement; `CustomerWindow` full CRUD; `AuditService` logging logins, customer
changes, and sales to an `AuditLog` table.

**Password change & user management — now implemented.** This used to be the
biggest documented gap; it no longer is:
- `ChangePasswordWindow` — any signed-in user can change their own password from
  the "Change password" link in the top bar of `MainWindow`. Requires the current
  password (`UserAccountService.ChangeOwnPasswordAsync`) and enforces the
  `Security.MinPasswordLength` policy (upper/lower/digit + minimum length).
- `UsersWindow` — Admin-only (`MainWindow` hides the "Users" nav item and card for
  non-admins via `IsAdmin`). Create users, change role (Admin/Cashier), activate/
  deactivate, and force-reset another user's password. Blocks an admin from
  deactivating or demoting their *own* account so you can't accidentally lock
  yourself out — have a second admin do that instead.
- Both windows write to `AuditLog` (`ChangePassword`, `Create`/`Update`/
  `ResetPassword` on `User`).
- So: sign in as `admin` / `ChangeMe123!`, immediately use "Change password" in
  the top bar, and use the Users screen to create named accounts for staff instead
  of sharing the admin login.

**Real receipt printing:** actual ESC/POS implementation — `EscPosBuilder` (raw
command bytes: initialize, code table, bold, double-size header, alignment, cut),
`NetworkPrinterClient` (raw TCP to port 9100, the standard network-thermal-printer
port). `ReceiptService` branches on `POS.PrinterConnection`: `"None"` (default,
text-file only), `"Network"` (real ESC/POS over TCP), `"WindowsShell"` (legacy,
Windows-only document printer). A failed print never blocks or reverses an
already-completed sale.

**Receipt reprint & sale voiding — now implemented.** `ReportsWindow` lists sale
history with a **Reprint** button (re-runs `ReceiptService` against the saved
sale and re-audits it) and a **Void** button (`SaleService.CancelAsync`, restores
the voided sale's stock, audited as `Void`). This closes the "no reprint UI" gap
from the previous pass.

**Barcode-scanner quick add.** The Sales search box now accepts a scan: a barcode
scanner types the code then sends Enter, which `SalesWindowViewModel
.TryQuickAddByCode()` catches — looks for an *exact* code match (not the
substring match the live filter uses, so a partial-code collision can't add the
wrong item), adds one unit to the cart, and clears the box for the next scan. If
nothing matches, it reports the miss in the status bar rather than silently doing
nothing; the box still works as a normal name/code filter either way.
*(This is the one gap this pass actually fixed — the KeyDown handler existed in
`SalesWindow.axaml.cs` calling this method, but the method itself was missing
from `SalesWindowViewModel.cs`, which would have failed to compile.)*

### Printer setup

1. In `appsettings.json`, set `POS.PrinterConnection` to `"Network"`.
2. Set `POS.PrinterIpAddress` to your printer's IP. Leave `PrinterPort` at `9100`
   unless your printer's manual says otherwise.
3. Run a test sale and check the printer.

### Two things that are genuinely printer-model-dependent

Still untested against real hardware — both called out in `EscPosBuilder.cs`:

- **`PrinterCodePage` (default `16`):** the code-table number for á/é/í/ó/ú/ñ/¿/¡
  isn't standardized across manufacturers. If accents print as garbage, try `0`
  or `2`, or check your printer's manual.
- **Cut command:** uses the classic one-parameter form (`GS V m`). A minority of
  clone printers expect the two-parameter form (`GS V 66 n`). Tell me your
  printer's exact model and I'll switch it.

## Setup

1. `CREATE DATABASE FastTPV;`
2. Copy `FastTPV.Desktop/appsettings.json.template` to
   `FastTPV.Desktop/appsettings.json`, fill in your real MySQL credentials, and
   set the printer fields above if you have a network thermal printer.
3. `dotnet restore && dotnet build && dotnet run --project FastTPV.Desktop`
4. Sign in with `admin` / `ChangeMe123!` on first launch, then immediately use
   "Change password" (top bar) to set a real password.

## Known limitations — not yet production-ready

- **No automated tests.** Nothing in this project has a test suite — the riskiest
  gap left, since every fix above has only been reviewed by reading, not run.
- **Not compiled or run** — no .NET SDK/NuGet access in the sandbox this was
  built in, again this pass. Run `dotnet build` yourself before relying on any
  of this, and specifically exercise: sign-in → change password → sign-out →
  sign-in with the new password; creating a user in `UsersWindow` and signing in
  as them; a barcode scan in Sales (or simulate one by typing a product's exact
  `Code` into the search box and pressing Enter); Reprint and Void from Reports.
- **Plaintext DB password** in `appsettings.json` — fine for one desktop install,
  not for shared machines or if this file is ever committed to source control.
- **Session timeout is approximate** (checked every 30s, not to-the-second).
- `Core/Data/ApplicationConfig.cs` from the original project was left out since
  it was dead code — `AppSettingsLoader.cs`/`RuntimeSettings` replaces it.
