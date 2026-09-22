# FastTPV — Production-Ready Rebuild

Your original `FastTPV` folder is untouched. This folder is the corrected, wired-up
build. Files superseded during any pass are renamed with a `.SUPERSEDED*` suffix —
**only the file WITHOUT such a suffix is current.** Delete the rest once copied in.

## This pass: a "FastTPV.NewFiles" folder was provided for reuse

16 files (models/services/viewmodels covering Suppliers, Stock Movements, split
Payments, Discounts, a second Cash Register system, and a Z-Report) were reviewed
file-by-file before integrating anything, because several directly conflicted with
or regressed already-working functionality. Here's exactly what happened to each:

### Adopted (after fixing real bugs)
- **`Supplier.cs` + `SupplierService.cs`** — clean model; service had a bug (see
  below), fixed. New `SupplierWindow` UI built from scratch (none was provided) and
  wired into the sidebar/dashboard.
- **`StockMovement.cs` + `StockMovementService.cs`** — audit trail for stock
  changes. The service's own stock-changing method did a fetch-then-full-row-UPDATE,
  which duplicates (less safely — no negative-stock guard, race-prone) what
  `ArticleService.AdjustStockAsync` already does atomically. Refactored so it
  delegates the real stock change to `ArticleService` and only adds the audit-log
  write. **Not yet wired into a UI** — see "Still to do" below.
- **`Payment.cs` + `PaymentService.cs`** — a real, valuable split/mixed-tender
  ledger (part cash, part card on one sale). Service had the same bug as
  `SupplierService` (below), fixed. **Not yet wired into Sales checkout UI** — the
  service is ready, the "let the cashier enter two amounts" screen isn't built yet.
- **`Category.cs`** — added the model and a `Categories` table. `Article.Category`
  is still a plain string, not a foreign key to this — nothing enforces articles to
  reference it yet. This is a placeholder for a future proper category system, not
  a completed feature.

### Rejected — duplicated something already built and working, better
- **`CashSession.cs` / `CashRegisterService.cs` / `CashRegisterViewModel.cs`** — a
  second, less capable cash-drawer system (no itemized cash-in/out log with
  reasons, no variance calc, and the same LAST_INSERT_ID bug below — its
  `OpenSessionAsync` would return null every single time against this project's
  actual `DatabaseContext`). The existing `CashSessionService`/`CashDrawerWindow`
  (built in an earlier pass, informed by the same competitor research you asked
  for on discounts) already does this properly. Not merged in.
- **`DiscountService.cs` / `PromotionService.cs`** — pure percentage-math helpers
  with no persistence, no approval-gating, no UI. The discount system already built
  into `SalesWindowViewModel` (line + ticket level, admin-approval above a
  threshold, persisted to `SaleLineItems.Discount`/`Sales.TicketDiscountAmount`)
  already does everything these attempted, and does it completely. Not merged in.

### Rejected wholesale, but the one good idea from each was kept
- **`SalesWindowViewModel.cs` (the new one)** — introduced split/mixed tendering,
  a genuinely good idea. But as written it: calls `SetProperty(...)`, a method that
  doesn't exist on ReactiveUI's `ReactiveObject` (wouldn't compile); **never calls
  stock adjustment, so a sale would never reduce inventory**; hardcodes Spain's 21%
  tax rate instead of the configurable one; drops customer linkage (always
  `CustomerId = 0`), barcode scanning, receipt printing, and audit logging. Not
  used. The split-tender *concept* is queued to be added to the existing, complete
  `SalesWindowViewModel` instead (see "Still to do").
- **`InventoryWindowViewModel.cs` (the new one)** — introduced "adjust stock with a
  reason," a genuinely good idea, logged via `StockMovementService`. But its
  property/command surface doesn't match the existing `InventoryWindow.axaml`'s
  bindings, so swapping it in would break that screen, and it drops the existing
  add/edit/delete form entirely. Not used. The adjust-with-reason capability is
  queued to be added onto the existing Inventory screen instead.
- **`ReportService.cs`** — mostly duplicates the existing `ReportsService`; its two
  extra methods (tax summary, sales summary) are worth having but don't need a
  second, confusingly-named class sitting next to the real one. Queued to merge in.
- **`CashRegisterViewModel.OpenDrawerAsync()`** — was a no-op stub with a comment
  saying it should talk to real hardware. It can, using the ESC/POS infrastructure
  already built for receipt printing (`GS p` — the standard cash-drawer-kick
  command most receipt printers with a drawer port support). Queued to implement.

### The bug pattern behind three of the rejected/fixed files
`CashRegisterService.OpenSessionAsync`, `PaymentService.AddAsync`, and
`SupplierService.AddAsync` (before fixing) all ran
`INSERT ...; SELECT LAST_INSERT_ID();` through `DatabaseContext.ExecuteQueryAsync` —
which only reads the *first* result set. An `INSERT` produces none, so the code got
zero rows back every time, meaning the row was actually inserted into the database
but the C# code always saw it as failing (or, in `CashRegisterService`'s case, threw
an index-out-of-range trying to read `rows[0]` of an empty list). This is exactly
why `ArticleService`/`CustomerService`/`SaleService` have their own
`ExecuteInsertAsync` helper — it uses `ExecuteScalarAsync` instead, which correctly
returns the value from whichever statement in a multi-statement batch actually
produced one. `SupplierService`/`PaymentService` are now fixed to use it.

## What's in this build overall (cumulative across all passes)

Sales + Inventory (search/cart/checkout/CRUD, barcode quick-add, line + ticket
discounts with admin-approval gating), Login/session/audit, User management with
roles, Customers, Suppliers, Reports (with Void/Reprint), Cash Drawer
(open/cash-in-out/close with variance), real ESC/POS network receipt printing,
Company Settings, Printer Settings with a hardware test-print button.

## Still to do (services are ready; UI wiring is not)

1. **Split-tender checkout UI** in `SalesWindow` — `PaymentService` is fixed and
   ready; needs a "Cash amount / Card amount" entry replacing (or sitting alongside)
   the current single-button Cash/Card/Digital checkout, computing and displaying
   change due.
2. **Manual stock-adjustment panel** in `InventoryWindow` — `StockMovementService`
   is fixed and ready; needs a small "+/- quantity, reason" UI element added to the
   existing screen (not a replacement of it) plus a "recent movements for this
   article" list.
3. **Cash-drawer hardware kick** — add a `KickDrawer()` method to `EscPosBuilder`
   (the `GS p m t1 t2` command) and wire a button to it, likely on the
   `CashDrawerWindow` and/or after a Cash sale completes.
4. **Merge `ReportService`'s tax/sales summary methods** into the existing
   `ReportsService` rather than keeping two classes.
5. Optionally, a proper **X/Z-Report** view (shift snapshot vs. end-of-day closing,
   standard POS/retail terminology) — natural to tie to Cash Drawer close-out.

## Setup

1. `CREATE DATABASE FastTPV;`
2. Copy `FastTPV.Desktop/appsettings.json.template` to
   `FastTPV.Desktop/appsettings.json`, fill in your real MySQL credentials.
3. `dotnet restore && dotnet build && dotnet run --project FastTPV.Desktop`
4. Sign in with `admin` / `ChangeMe123!`, then immediately use "Change password."
5. If you have a network thermal printer: **Printer Settings** → Network + IP →
   **Send test print** to verify before saving.
6. **Company Settings** → your real business details for receipts.

## Known limitations — not yet production-ready

- **Not compiled or run, still** — no .NET SDK/NuGet access in this sandbox, across
  every pass. Given the concrete compile-errors and functional regressions found
  in reviewed-but-rejected files this pass, and the ones caught in earlier passes
  (a nonexistent Avalonia type, duplicate-class errors), treat this as unverified
  until you've run `dotnet build` yourself.
- **No automated tests.**
- **No user-facing "forgot password" recovery** — an Admin resets it via Users.
- **Plaintext DB password** in `appsettings.json`.
- **Session timeout is approximate** (checked every 30s).
- **`Article.Category` is a string, not linked to the new `Categories` table.**
- `Core/Data/ApplicationConfig.cs` from the original project was left out (dead code).

## Deferred — need your input before building, not just code gaps

- **Automated tests, CI/CD, an installer** — need your infra choices.
- **Offline mode, multi-store support, Spanish fiscal compliance (VeriFactu/AEAT)**
  — each a substantial architectural project on its own; explicitly parked per your
  instruction to focus on finishing FastTPV itself first.
