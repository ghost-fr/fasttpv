# FastTPV — Production-Ready Rebuild

Your original `FastTPV` folder is untouched. This folder is the corrected, wired-up
build. Files superseded during any pass are renamed with a `.SUPERSEDED*` suffix —
**only the file WITHOUT such a suffix is current.** Delete the rest once copied in.

## Two "new files" folders have now been reviewed and partially adopted

Both were reviewed file-by-file before integrating anything, because significant
parts directly conflicted with, duplicated, or regressed already-working
functionality. If a third batch shows up, apply the same standard: read every file,
check it compiles against what's *actually* in this project (constructor
signatures, method names, reserved SQL words), and only adopt what's genuinely new
and correct.

### Folder 1 ("FastTPV.NewFiles") — adopted: Suppliers, Stock Movements, Payments model
- **Kept & fixed:** `Supplier`/`SupplierService` (had a `LAST_INSERT_ID()` bug, fixed),
  `StockMovement`/`StockMovementService` (refactored to delegate the actual stock
  change to `ArticleService.AdjustStockAsync` rather than duplicate unsafe logic),
  `Payment` model, `Category` model/table (not yet linked to `Article.Category`).
  New `SupplierWindow` built from scratch and wired into the sidebar.
- **Rejected — duplicated something already built better:** a second `CashSession`/
  `CashRegisterService`/`CashRegisterViewModel` (no itemized cash-in/out log, same
  DB bug below) and `DiscountService`/`PromotionService` (bare percentage math, no
  approval-gating, no persistence, no UI — the discount system already in
  `SalesWindowViewModel` does all of this).
- **Rejected wholesale — real bugs:** its `SalesWindowViewModel` called
  `SetProperty(...)`, which doesn't exist on ReactiveUI's `ReactiveObject` (wouldn't
  compile), never adjusted stock after a sale, and hardcoded a 21% tax rate. Its
  `InventoryWindowViewModel` didn't match the existing screen's bindings.
- **The recurring bug:** `CashRegisterService`, `PaymentService`, and
  `SupplierService` all ran `INSERT ...; SELECT LAST_INSERT_ID();` through
  `DatabaseContext.ExecuteQueryAsync`, which only reads the *first* result set — an
  `INSERT` produces none, so the code always got 0 rows back (row inserted, ID
  never retrieved; `CashRegisterService` even threw on `rows[0]` of an empty list).
  Fixed by using the existing `ExecuteInsertAsync` helper instead, exactly as
  `ArticleService`/`CustomerService`/`SaleService` already do.

### Folder 2 ("newfiles") — adopted: cash-drawer hardware kick only
This folder largely re-submitted the same `SalesWindowViewModel`/
`InventoryWindowViewModel`/`PaymentService` bugs from Folder 1 (same `SetProperty`
call, same `LAST_INSERT_ID()` pattern — plus a *new* bug: `Change` used unquoted in
a raw SQL INSERT, which is a MySQL reserved word and would throw a syntax error).
It was also internally inconsistent: its own `InventoryWindow.axaml.cs` called
`vm.AdjustQuantity`, `vm.AdjustReason`, and `vm.AdjustSelectedStockAsync()` — none
of which exist on that same folder's own `InventoryWindowViewModel.cs`. None of
that was adopted. What *was* genuinely new and correct: `EscPosBuilder.KickDrawer()`
— the standard Epson-compatible cash-drawer pulse command (`ESC p m t1 t2`). Added
to the existing `EscPosBuilder`, plus a new `ReceiptService.KickDrawerAsync()` and
a "🔓 Kick drawer" button in `CashDrawerWindow`. The drawer is now also kicked
automatically at the end of a Cash-tendered sale (right after the receipt cuts).

## What's in this build overall (cumulative across all passes)

Sales + Inventory (search/cart/checkout/CRUD, barcode quick-add, line + ticket
discounts with admin-approval gating), Login/session/audit, User management with
roles, Customers, Suppliers, Reports (with Void/Reprint), Cash Drawer
(open/cash-in-out/close with variance, hardware drawer kick), real ESC/POS network
receipt printing, Company Settings, Printer Settings with a hardware test-print
button.

## Still to do (services are ready; UI wiring is not)

1. **Split-tender checkout UI** in `SalesWindow` — `PaymentService` is fixed and
   ready; needs a "Cash amount / Card amount" entry, computing and displaying
   change due. (Two independent attempts at this have now been reviewed and
   rejected for bugs — if a third arrives, check it especially carefully.)
2. **Manual stock-adjustment panel** in `InventoryWindow` — `StockMovementService`
   is fixed and ready; needs a small "+/- quantity, reason" UI element added to the
   existing screen (not a replacement of it).
3. **Merge `ReportService`'s tax/sales summary methods** into the existing
   `ReportsService` rather than keeping two classes (Folder 1 also included a
   `ReportService.cs` with two useful read-only methods not yet merged in).
4. Optionally, a proper **X/Z-Report** view — natural to tie to Cash Drawer
   close-out.

## Setup

1. `CREATE DATABASE FastTPV;`
2. Copy `FastTPV.Desktop/appsettings.json.template` to
   `FastTPV.Desktop/appsettings.json`, fill in your real MySQL credentials.
3. `dotnet restore && dotnet build && dotnet run --project FastTPV.Desktop`
4. Sign in with `admin` / `ChangeMe123!`, then immediately use "Change password."
5. If you have a network thermal printer: **Printer Settings** → Network + IP →
   **Send test print** to verify before saving. If it has a cash drawer wired to
   it, try **Cash Drawer → Kick drawer** too.
6. **Company Settings** → your real business details for receipts.

## Known limitations — not yet production-ready

- **Not compiled or run, still** — no .NET SDK/NuGet access in this sandbox, across
  every pass. Given the concrete, repeated compile-errors and functional
  regressions found in reviewed-but-rejected files across two separate "new files"
  batches, treat this as unverified until you've run `dotnet build` yourself.
- **No automated tests.**
- **No user-facing "forgot password" recovery** — an Admin resets it via Users.
- **Plaintext DB password** in `appsettings.json`.
- **Session timeout is approximate** (checked every 30s).
- **`Article.Category` is a string, not linked to the `Categories` table.**
- `Core/Data/ApplicationConfig.cs` from the original project was left out (dead code).

## Deferred — need your input before building, not just code gaps

- **Automated tests, CI/CD, an installer** — need your infra choices.
- **Offline mode, multi-store support, Spanish fiscal compliance (VeriFactu/AEAT)**
  — each a substantial architectural project on its own; explicitly parked per your
  instruction to focus on finishing FastTPV itself first.
