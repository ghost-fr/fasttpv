# FastTPV UI redesign — what this is and how to apply it

This is a real code patch, not a mockup. Every binding in these files points at
properties and commands that already exist in your `SalesWindowViewModel` and
`MainWindowViewModel` — nothing here invents sample products, fake sales
totals, or placeholder numbers. The only new "data" is two small converters
that read an article's own `StockLevel` / `MinimumStock` fields to decide
whether to show a low-stock pill.

A live version of the same design (colors, type, components, Home, Sales) is
also in your new Figma file, **FastTPV – POS Redesign**, if you want to look
at it or hand it to someone else before touching code.

## Files to copy into your project

Copy each file to the same path inside `FastTPV.Desktop/`, overwriting the
existing one (back up first if you want to compare):

| File | What changed |
|---|---|
| `Program.cs` | Sets the app's culture to `es-ES` at startup, so every existing `{0:C}` price/total binding formats as euros (`1.234,50 €`) instead of the OS locale's currency. This is what was showing "Rs". |
| `App.axaml` | Pulls in the two new resource files below, on top of your existing FluentTheme. |
| `Styles/Tokens.axaml` | **New file.** Color and icon definitions used by the redesign. |
| `Styles/Theme.axaml` | **New file.** Dark-theme styles, all opt-in via `Classes="..."` — windows you haven't touched (Inventory, Customers, Suppliers, Reports, Users, Settings) are untouched and keep working exactly as before. |
| `Converters/StockStatusConverters.cs` | **New file.** Two tiny converters that flag low stock using the article's own `MinimumStock`, and format the stock label. |
| `Views/MainWindow.axaml` | Redesigned Home screen: dark theme, icons, fixed the clipped tile text, single "Start a new sale" hero. Same commands/bindings as before (`OpenSalesCommand`, `IsAdmin`, `StatusMessage`, etc.) — nothing was renamed. |
| `Views/SalesWindow.axaml` | Redesigned Sales screen: category chips, bigger product tiles with the real low-stock pill, a real cart panel, large Cash/Card/Digital buttons, and split payment hidden behind a "Split payment" toggle instead of always showing. Same `SalesWindowViewModel` bindings and the same `SearchKeyDown` handler — `SalesWindow.axaml.cs` does not need to change. |

`MainWindow.axaml.cs` and `SalesWindow.axaml.cs` are unchanged — the redesign
is entirely in XAML/resources plus the one `Program.cs` culture line.

## Steps

1. Copy the files above into your local checkout at the matching paths.
2. `dotnet build` from `FastTPV.Desktop/`. I wasn't able to compile this myself —
   my sandbox can't reach `nuget.org` to restore your packages — so treat this
   as a strong draft, not a guaranteed-clean build. If you get an XAML error,
   paste it here and I'll fix that line.
3. `dotnet run --project FastTPV.Desktop`, open Home, then Sales, and check:
   - Prices show `€`, not `Rs`.
   - All tile text is fully visible (no more clipped labels).
   - The Sales screen's Cash/Card/Digital buttons are large and bright.
   - "Split payment" only shows the split fields once you tap it.
4. Send me new screenshots and I'll do another pass, or start wiring up the
   next thing (Spanish translation, the other windows, fiscal compliance).

## What I deliberately left out

- No fabricated dashboard numbers (today's sales, ticket count, etc.) on
  Home — `MainWindowViewModel` doesn't currently compute those, and I didn't
  want to hardcode figures that aren't real. If you want a real stats strip
  on Home, that needs a few new properties in `MainWindowViewModel` pulled
  from `SaleService`/`CashDrawerService` — happy to build that next.
- No "which nav item is active" highlight in the sidebar, since Sales,
  Inventory, etc. each open as their own window rather than a page inside
  Main — there's no "current page" concept yet to highlight.
