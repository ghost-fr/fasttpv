using FastTPV.Core.Models;
using FastTPV.Core.Services;
using Xunit;

namespace FastTPV.Tests;

/// <summary>Database-free tests for the checkout validation rules (tender, change, line totals).</summary>
public class CheckoutPlannerTests
{
    private static Sale Ticket(decimal unit = 10m, int qty = 2, decimal discount = 0m, decimal ticketTotal = -1m)
    {
        var line = new SaleLineItem
        {
            ArticleId = 1, ArticleName = "Widget", Quantity = qty, UnitPrice = unit,
            Discount = discount, LineTotal = unit * qty - discount
        };
        var sale = new Sale { SubTotal = line.LineTotal, TotalAmount = ticketTotal >= 0 ? ticketTotal : line.LineTotal };
        sale.LineItems.Add(line);
        return sale;
    }

    private static PaymentLine[] Pay(params (string m, decimal a)[] p) => p.Select(x => new PaymentLine(x.m, x.a)).ToArray();

    [Fact]
    public void Exact_cash_is_valid_with_no_change()
    {
        var plan = CheckoutPlanner.Validate(Ticket(), Pay(("Cash", 20m)));
        Assert.True(plan.IsValid);
        Assert.Equal(0m, plan.Change);
        Assert.Equal("Cash", plan.PaymentMethod);
    }

    [Fact]
    public void Overpaying_cash_returns_change()
    {
        var plan = CheckoutPlanner.Validate(Ticket(), Pay(("Cash", 50m)));
        Assert.True(plan.IsValid);
        Assert.Equal(30m, plan.Change);
    }

    [Fact]
    public void Short_tender_is_rejected()
    {
        var plan = CheckoutPlanner.Validate(Ticket(), Pay(("Cash", 5m), ("Card", 10m)));
        Assert.False(plan.IsValid);
        Assert.Equal(CheckoutStatus.PaymentInvalid, plan.ErrorStatus);
    }

    [Fact]
    public void Split_tender_is_labelled_Mixed()
    {
        var plan = CheckoutPlanner.Validate(Ticket(), Pay(("Cash", 5m), ("Card", 15m)));
        Assert.True(plan.IsValid);
        Assert.Equal("Mixed", plan.PaymentMethod);
    }

    [Fact]
    public void Change_cannot_come_from_card_overpayment()
    {
        // Total 20, card 25 and no cash: no cash drawer money to hand back.
        var plan = CheckoutPlanner.Validate(Ticket(), Pay(("Card", 25m)));
        Assert.False(plan.IsValid);
        Assert.Contains("cash", plan.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Change_larger_than_cash_tendered_is_rejected()
    {
        // Total 20: card 30 + cash 5 => change 15 but only 5 cash was given.
        var plan = CheckoutPlanner.Validate(Ticket(), Pay(("Card", 30m), ("Cash", 5m)));
        Assert.False(plan.IsValid);
    }

    [Fact]
    public void Zero_amount_lines_are_dropped_and_duplicates_merged()
    {
        var plan = CheckoutPlanner.Validate(Ticket(), Pay(("Cash", 10m), ("cash", 10m), ("Card", 0m)));
        Assert.True(plan.IsValid);
        Assert.Single(plan.Payments);
        Assert.Equal(20m, plan.Payments[0].Amount);
    }

    [Fact]
    public void Zero_total_ticket_needs_no_payment()
    {
        var plan = CheckoutPlanner.Validate(Ticket(unit: 10m, qty: 1, discount: 10m), Array.Empty<PaymentLine>());
        Assert.True(plan.IsValid);
    }

    [Fact]
    public void Empty_ticket_is_rejected()
    {
        var plan = CheckoutPlanner.Validate(new Sale(), Pay(("Cash", 1m)));
        Assert.False(plan.IsValid);
        Assert.Equal(CheckoutStatus.Invalid, plan.ErrorStatus);
    }

    [Fact]
    public void Bad_quantities_discounts_and_totals_are_rejected()
    {
        Assert.False(CheckoutPlanner.Validate(Ticket(qty: 0), Pay(("Cash", 20m))).IsValid);

        var negDiscount = Ticket();
        negDiscount.LineItems[0].Discount = -1m;
        Assert.False(CheckoutPlanner.Validate(negDiscount, Pay(("Cash", 20m))).IsValid);

        var tooBig = Ticket();
        tooBig.LineItems[0].Discount = 999m;
        Assert.False(CheckoutPlanner.Validate(tooBig, Pay(("Cash", 20m))).IsValid);

        var tampered = Ticket();
        tampered.LineItems[0].LineTotal = 1m; // does not equal price x qty - discount
        Assert.False(CheckoutPlanner.Validate(tampered, Pay(("Cash", 20m))).IsValid);
    }
}
