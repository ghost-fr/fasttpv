# FastTPV — Production-Ready Rebuild

Your original `FastTPV` folder is untouched. This folder is the corrected, wired-up
build. Files superseded during any pass are renamed with a `.SUPERSEDED*` suffix —
**only the file WITHOUT such a suffix is current.** Delete the rest once copied in.

## The four remaining "still to do" items are now all done

All four items from the previous README's "Still to do" list have been completed
and wired up end-to-end:

1. **Split-tender checkout UI** — `SalesWindow` now has Cash amount / Card amount
   fields, a "Complete split payment" button (`CompleteSplitSaleCommand`), and a
   live status line ("Tendered X — change due Y / short Y / exact") bound to the
   ViewModel's `SplitStatusText`. Backed by `PaymentService.RecordPaymentsAsync`,
   which writes one `Payments` row per tender method and computes change.
   *Fixed while re-verifying this:* the status line's XAML had been wired through a
   redundant `MultiBinding` with a no-op `Converter="{x:Null}"` that lost the
   "change due"/"short"/"exact" wording and just printed a raw decimal — replaced
   with a direct bind to `SplitStatusText`, which already computed the right text.
2. **Manual stock-adjustment panel** in `InventoryWindow` — a new "Adjust Stock"
   card next to the Add/Edit form: quantity (+/−) and a required reason, wired to
   `StockMovementService.AdjustStockWithReasonAsync` via a new `AdjustStockCommand`
   on `InventoryWindowViewModel`. Applies the change atomically (guarded against
   going below zero, same as everywhere else), logs who/why to `StockMovements`,
   then refreshes the grid and re-selects the same article.
3. **Merged `ReportService`'s summary methods into `ReportsService`** —
   `GetSalesSummaryAsync` (gross/discount/tax/net totals) and
   `GetPaymentMethodBreakdownAsync` (totals by Cash/Card/Digital/Mixed) now live on
   the one `ReportsService` class. (This merge had already been done in an earlier
   pass this session — confirmed correct on review.)
4. **X/Z-Report view** — `ReportsWindow` now has a "Register Summary (X/Z Report)"
   panel using the two methods above, for whichever From/To range the existing
   date pickers are set to. *Gap found and fixed while completing this item:* the
   XAML for this panel had been added (with named elements `SummaryGross`,
   `SummaryDiscount`, `SummaryTax`, `SummaryNet`, `PaymentBreakdownList`) but
   `ReportsWindow.axaml.cs`'s `LoadAsync()` was never updated to populate them —
   the panel would have always shown "0.00" and an empty list. Added the two
   service calls and the assignments; one `Refresh` now updates everything on the
   screen together.

There is no remaining "services ready, UI not wired" gap — everything built across
every pass this session is now reachable from the UI.

## What's in this build overall (cumulative across all passes)

Sales + Inventory (search/cart/checkout with single- and split-tender, barcode
quick-add, line + ticket discounts with admin-approval gating, manual stock
adjustment with reason), Login/session/audit, User management with roles,
Customers, Suppliers, Reports (sales history, low-stock, Void/Reprint, X/Z
register summary with payment-method breakdown), Cash Drawer (open/cash-in-out/
close with variance, hardware drawer kick), real ESC/POS network receipt
printing, Company Settings, Printer Settings with a hardware test-print button.

## Two "new files" folders were reviewed and partially adopted earlier this project

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
  Everything in this pass (split-tender, stock adjustment, X/Z-Report wiring) is
  also unverified for the same reason — review the diffs before relying on it.
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
