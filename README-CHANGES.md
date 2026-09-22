# FastTPV — Production-Ready Rebuild

Your original `FastTPV` folder is untouched. This folder is the corrected, wired-up
build. Files superseded during any pass are renamed with a `.SUPERSEDED*` suffix —
**only the file WITHOUT such a suffix is current.** Delete the rest once copied in.

## This pass: production hardening

You asked to make this production-ready. Worked through security, known data/logic
gaps, and missing safety nets — everything below is new this pass:

### Security
- **DB password no longer has to live in a plaintext file.** `RuntimeSettings.Load`
  now checks the environment variable `FASTTPV_CONNECTION_STRING` after parsing
  appsettings.json, and uses it if set — it always wins over the file. Set it at
  the OS/service level in production (systemd unit, Docker Compose env, Windows
  service config) and leave `appsettings.json`'s `ConnectionStrings.DefaultConnection`
  as a harmless placeholder. Documented in a `_comment_production` note at the top
  of `appsettings.json.template`. This doesn't erase the limitation — if you don't
  set the env var, the password is exactly as exposed as before — it just gives you
  a way to avoid it.
- **Account lockout.** `Users` gained `FailedLoginAttempts` and `LockedUntil`
  columns (added via the same additive `TryAlterAsync`/error-1060 pattern the
  schema already uses, so this is safe to run against an existing database).
  `UserAccountService.AuthenticateAsync` now increments the counter on a wrong
  password and, after `MaxFailedLoginAttempts` (default 5, configurable in
  `Security` in appsettings.json), locks the account for `LockoutMinutes` (default
  15). A correct password resets both. `LoginWindow` shows a specific "try again in
  N minutes" message during a lockout via the new
  `GetLockoutRemainingAsync`, and any other failure still gets the same generic
  message as before (so a bad guess can't be used to fingerprint which case it is).
  An Admin resetting a user's password (`SetPasswordAsync`) also clears the lockout.

### Known data/logic gap: Category now genuinely linked
- New `CategoryService` (Core/Services) does CRUD on the `Categories` table, plus
  `EnsureExistsAsync` — called every time `InventoryWindowViewModel.SaveAsync`
  saves a product, so whatever category name was typed is automatically registered
  (or re-activated) there. `InventoryWindow`'s Category field is now an
  `AutoCompleteBox` sourced from `CategoryService.GetActiveNamesAsync`, wired
  through `AppRuntime.Categories`.
  **What this is and isn't:** `Article.Category` is still a plain string column,
  not a foreign key — changing that would mean an `ALTER TABLE` to add a nullable
  `CategoryId`, backfilling it from the existing string values, and updating every
  query that filters/joins on Category, which is a real migration I didn't want to
  run blind with no way to verify it against a live database in this sandbox. What
  you have now: the Categories table is the actual source of truth for "which
  category names exist," names typed once are offered as autocomplete everywhere
  after, and there's a `DeactivateAsync` for retiring a name without breaking
  products still tagged with it. A true FK migration is still a clean, well-scoped
  follow-up if you want it — just flag it and I'll do the schema migration properly
  with a backfill step.

### Reviewed, not changed: session timeout
Checked `MainWindow.axaml.cs`'s activity tracking — `PointerMoved`/`KeyDown` call
`SessionManager.Touch()`, and a 30-second timer calls `Check()` against
`AutoLogoffMinutes`. This is correct as designed; "approximate" in the prior
README meant "worst case up to 30 seconds late," which is an inherent trade-off
of interval-based polling, not a bug. Left as-is.

### Input validation pass
`CustomerWindow`'s Save now rejects a negative Credit Limit and a
non-plausible email address (must have an `@` with characters on both sides and a
`.` in the domain — deliberately loose, not full RFC 5322, just enough to catch
typos before they hit the database) before writing to the DB. Article/Inventory
already had `Minimum="0"` guards on all numeric fields from an earlier pass.

## Still open — deliberately not done without your input
- **Self-service "forgot password"** — the safety net added this pass is account
  lockout, not password recovery. A real forgot-password flow needs an email
  service (SMTP relay + templates) or an out-of-band code path, which means new
  infrastructure and credentials only you can decide on. Admin-reset via
  `UsersWindow` still covers this for now.
- **`Article.Category` → `CategoryId` foreign key** — see above; scoped and ready
  to do, just say the word.
- Everything already listed below under "Deferred."

## What's in this build overall (cumulative across all passes)

Sales + Inventory (search/cart/checkout with single- and split-tender, barcode
quick-add, line + ticket discounts with admin-approval gating, manual stock
adjustment with reason, category autocomplete), Login/session/audit with account
lockout, User management with roles, Customers (with basic input validation),
Suppliers, Reports (sales history, low-stock, Void/Reprint, X/Z register summary
with payment-method breakdown), Cash Drawer (open/cash-in-out/close with
variance, hardware drawer kick), real ESC/POS network receipt printing, Company
Settings, Printer Settings with a hardware test-print button.

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
  `Payment` model, `Category` model/table (now actually linked — see above).
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
   `FastTPV.Desktop/appsettings.json`. For local dev, fill in your real MySQL
   credentials there directly. For production, leave the placeholder and instead
   set the `FASTTPV_CONNECTION_STRING` environment variable — see the
   `_comment_production` note at the top of the template.
3. `dotnet restore && dotnet build && dotnet run --project FastTPV.Desktop`
4. Sign in with `admin` / `ChangeMe123!`, then immediately use "Change password."
5. If you have a network thermal printer: **Printer Settings** → Network + IP →
   **Send test print** to verify before saving. If it has a cash drawer wired to
   it, try **Cash Drawer → Kick drawer** too.
6. **Company Settings** → your real business details for receipts.

## Known limitations — not yet production-ready

- **Not compiled or run, still** — no .NET SDK/NuGet access in this sandbox
  (installed the SDK itself this pass, but the container's network allowlist
  doesn't include `api.nuget.org`, so packages like Avalonia/MySql.Data/
  ReactiveUI can't be restored — a hard constraint, not something I can work
  around from here). Given the concrete, repeated compile-errors and functional
  regressions found in reviewed-but-rejected files across two separate "new files"
  batches, treat every pass — including this one — as unverified until you've run
  `dotnet build` yourself.
- **No automated tests.**
- **No user-facing "forgot password" recovery** — see "Still open" above.
- **`Article.Category` is a string, not a foreign key to `Categories`** — see
  "Still open" above for what's actually in place now vs. what a full migration
  would add.
- **Session timeout has up to ~30s of slack** — by design, see above.
- `Core/Data/ApplicationConfig.cs` from the original project was left out (dead code).

## Deferred — need your input before building, not just code gaps

- **Automated tests, CI/CD, an installer** — need your infra choices.
- **Offline mode, multi-store support, Spanish fiscal compliance (VeriFactu/AEAT)**
  — each a substantial architectural project on its own; explicitly parked per your
  instruction to focus on finishing FastTPV itself first.
- **Self-service forgot-password** — needs an email/SMTP decision; see "Still open."
