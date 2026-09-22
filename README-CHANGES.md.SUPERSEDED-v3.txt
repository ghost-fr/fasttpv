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

**Login, Customers, Reports, Audit (`Features/` folder):** `AppRuntime`
composition root; `LoginWindow` with PBKDF2-hashed passwords against a new
`Users` table (first-run bootstrap seeds `admin` / `ChangeMe123!` — **change
this immediately**, there's no password-change UI yet); session idle-timeout
enforcement; `CustomerWindow` full CRUD; `ReportsWindow` (sales-in-range +
low-stock); `AuditService` logging logins, customer changes, and sales to a new
`AuditLog` table.

**Real receipt printing (this pass):** replaced the Windows-only "shell out to
print a text file" stopgap with an actual ESC/POS implementation:
- `EscPosBuilder` — builds the raw byte command stream (initialize, character
  code table, bold, double-size header, alignment, line feed, cut) using
  standard, documented Epson ESC/POS commands that the overwhelming majority
  of "ESC/POS compatible" thermal printers implement regardless of brand.
- `NetworkPrinterClient` — sends those bytes over a raw TCP socket to your
  printer's IP address, port 9100 (the near-universal default for network/
  Ethernet/WiFi thermal printers — sometimes called "JetDirect" or "AppSocket"
  printing; no HTTP, just a socket write).
- `ReceiptService` now branches on `POS.PrinterConnection`:
  - `"None"` (default) — writes the text-file receipt only, no hardware
    attempt, so checkout never waits on a printer that doesn't exist.
  - `"Network"` — the real path: builds ESC/POS bytes and sends them to
    `POS.PrinterIpAddress:PrinterPort`.
  - `"WindowsShell"` — the old fallback, kept for a regular (non-thermal)
    document printer, Windows-only.
- A failed print (printer off, wrong IP, network down) never blocks or
  reverses an already-completed sale — it's logged and checkout continues.
  The text file in `%LocalAppData%/FastTPV/Receipts/` is always written and is
  the source of truth either way.

### Printer setup

1. In `appsettings.json`, set `POS.PrinterConnection` to `"Network"`.
2. Set `POS.PrinterIpAddress` to your printer's IP (check the printer's self-test
   page or your router's connected-devices list). Leave `PrinterPort` at `9100`
   unless your printer's manual says otherwise.
3. Run a test sale and check the printer.

### Two things that are genuinely printer-model-dependent

I could not test any of this against real hardware, so be aware of exactly two
soft spots, both called out in `EscPosBuilder.cs`:

- **`PrinterCodePage` (default `16`):** the numeric code that selects a
  Windows-1252-equivalent character table (needed for á/é/í/ó/ú/ñ/¿/¡) is
  **not standardized** across manufacturers. If accented characters print as
  garbled symbols or boxes, this is the first thing to change — try `0` or `2`,
  or check your printer's manual for its character code table list.
- **Cut command:** uses the classic one-parameter form (`GS V m`). A minority
  of clone printers instead expect the newer two-parameter form (`GS V 66 n`).
  If everything prints correctly but the paper isn't cut, that's why — tell me
  your printer's exact model and I'll switch the command.

If you tell me your printer's brand/model, I can look up its actual command
reference and set correct defaults instead of the generic ones above.

## Setup

1. `CREATE DATABASE FastTPV;`
2. Copy `FastTPV.Desktop/appsettings.json.template` to
   `FastTPV.Desktop/appsettings.json`, fill in your real MySQL credentials, and
   set the printer fields as above if you have a network thermal printer.
3. `dotnet restore && dotnet build && dotnet run --project FastTPV.Desktop`
4. Sign in with `admin` / `ChangeMe123!` on first launch and change it (see gap
   below).

## Known limitations — not yet production-ready

- **No password-change or user-management UI** — the single biggest remaining
  gap. New accounts or password changes currently mean writing directly to the
  `Users` table using the same PBKDF2 hash/salt shape
  `UserAccountService.HashPassword` produces.
- **No automated tests.**
- **Not compiled or run** — no .NET SDK/NuGet access in the sandbox this was
  built in. Run `dotnet build` yourself. Specifically worth testing manually:
  the Login → Main window handoff (confirm the app doesn't quit when the login
  window closes after a successful sign-in — this depends on the
  `ShutdownMode` change in `App.axaml.cs`), and an actual print if you have the
  hardware, given the two printer-dependent items noted above.
- **Plaintext DB password** in `appsettings.json` — fine for one desktop
  install, not for shared machines.
- **Receipt printing has no receipt preview/reprint UI** — the text file is
  saved to disk for manual reference, but there's no "reprint this ticket"
  button anywhere in the app.
- **Session timeout is approximate** (checked every 30s, not to-the-second).
- `Core/Data/ApplicationConfig.cs` from the original project was left out
  since it was dead code — `AppSettingsLoader.cs`/`RuntimeSettings` replaces it.
