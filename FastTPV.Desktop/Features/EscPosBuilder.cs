using System.Text;

namespace FastTPV.Desktop.Features;

public enum TextAlign { Left, Center, Right }

/// <summary>
/// Which paper-cut command variant to send. There is no reliable brand→command
/// mapping: ESC/POS "compatibility" firmware varies even across models from the
/// same manufacturer and across firmware revisions of the same model. Rather than
/// hardcoding guesses, PrinterSettingsWindow lets whoever installs FastTPV pick one
/// and confirm it with "Send test print" against the actual hardware in front of them.
/// </summary>
public enum CutCommandStyle
{
    /// <summary>GS V m — the classic one-parameter form. Correct for the large
    /// majority of ESC/POS-compatible printers; the right first thing to try.</summary>
    Standard,

    /// <summary>GS V 66 n — the newer two-parameter form some clone/older firmware
    /// expects instead. Try this if the receipt prints correctly but the paper
    /// isn't cut afterward.</summary>
    TwoParameter,

    /// <summary>No cut command is sent at all — for printers with no cutter, or
    /// where the operator tears the paper by hand.</summary>
    None
}

/// <summary>
/// Builds a raw ESC/POS byte stream for thermal receipt printers. Implements the
/// small subset of the (widely cloned) Epson ESC/POS command set needed for a
/// simple receipt: initialize, character code table, alignment, bold, double-size
/// text, line feed, cut, and a cash-drawer kick pulse.
///
/// The commands themselves are standard, documented ESC/POS — implemented by the
/// overwhelming majority of "ESC/POS compatible" thermal printers regardless of
/// brand. Two things are still genuinely printer/firmware-dependent: the code page
/// number (SelectCodePage) and the cut command variant (Cut) — both are exposed as
/// settings in PrinterSettingsWindow with a "Send test print" button, rather than
/// guessed here, since neither can be reliably inferred from a printer's brand/model.
/// </summary>
public sealed class EscPosBuilder
{
    private readonly List<byte> _bytes = new();
    private readonly Encoding _encoding;

    public EscPosBuilder(Encoding encoding)
    {
        _encoding = encoding;
        _bytes.AddRange(new byte[] { 0x1B, 0x40 }); // ESC @ — initialize printer
    }

    public EscPosBuilder SelectCodePage(int n)
    {
        _bytes.AddRange(new byte[] { 0x1B, 0x74, (byte)n }); // ESC t n
        return this;
    }

    public EscPosBuilder Align(TextAlign align)
    {
        byte n = align switch { TextAlign.Center => 1, TextAlign.Right => 2, _ => 0 };
        _bytes.AddRange(new byte[] { 0x1B, 0x61, n }); // ESC a n
        return this;
    }

    public EscPosBuilder Bold(bool on)
    {
        _bytes.AddRange(new byte[] { 0x1B, 0x45, (byte)(on ? 1 : 0) }); // ESC E n
        return this;
    }

    public EscPosBuilder DoubleSize(bool on)
    {
        _bytes.AddRange(new byte[] { 0x1D, 0x21, (byte)(on ? 0x11 : 0x00) }); // GS ! n (double width+height)
        return this;
    }

    public EscPosBuilder Text(string text)
    {
        _bytes.AddRange(_encoding.GetBytes(text));
        return this;
    }

    public EscPosBuilder Line(string text = "")
    {
        Text(text);
        _bytes.Add(0x0A);
        return this;
    }

    public EscPosBuilder Feed(int lines = 1)
    {
        _bytes.AddRange(new byte[] { 0x1B, 0x64, (byte)lines }); // ESC d n
        return this;
    }

    public EscPosBuilder Cut(CutCommandStyle style = CutCommandStyle.Standard, bool partial = true)
    {
        switch (style)
        {
            case CutCommandStyle.Standard:
                _bytes.AddRange(new byte[] { 0x1D, 0x56, (byte)(partial ? 1 : 0) }); // GS V m
                break;
            case CutCommandStyle.TwoParameter:
                _bytes.AddRange(new byte[] { 0x1D, 0x56, 0x42, (byte)(partial ? 1 : 0) }); // GS V 66 n
                break;
            case CutCommandStyle.None:
                // No cutter, or the operator tears the paper by hand — send nothing.
                break;
        }
        return this;
    }

    /// <summary>
    /// Sends the standard Epson-compatible cash-drawer pulse (ESC p m t1 t2) — most
    /// thermal receipt printers have an RJ11/RJ12 port that a cash drawer plugs into
    /// and relay this command through. pin selects which of the two drawer connector
    /// pins to pulse (0 is by far the most common wiring; try 1 if nothing happens).
    /// t1/t2 are the on/off pulse duration in ~2ms units — the defaults (25/250,
    /// i.e. ~50ms on/~500ms off) match Epson's documented default and work with the
    /// vast majority of drawers. Sending this to a printer with no drawer attached
    /// is harmless — it's simply ignored.
    /// </summary>
    public EscPosBuilder KickDrawer(byte pin = 0, byte onTimeUnits = 25, byte offTimeUnits = 250)
    {
        _bytes.AddRange(new byte[] { 0x1B, 0x70, pin, onTimeUnits, offTimeUnits }); // ESC p m t1 t2
        return this;
    }

    public byte[] ToArray() => _bytes.ToArray();

    /// <summary>Parses the string stored in appsettings.json (POS.PrinterCutStyle)
    /// into the enum, defaulting to Standard for anything unrecognized (including
    /// an empty/missing value) so an old settings file without this key still works.</summary>
    public static CutCommandStyle ParseCutStyle(string? value) => value switch
    {
        "TwoParameter" => CutCommandStyle.TwoParameter,
        "None" => CutCommandStyle.None,
        _ => CutCommandStyle.Standard
    };
}
