using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace BatoBuzz.Desktop.Services;

/// <summary>
/// Small reusable builder for the letterhead + info-block + table + totals
/// layout shared by voucher print-outs (invoices, bills, receipts, payments,
/// journal vouchers). Callers compose these primitives per voucher shape
/// rather than each hand-rolling PdfSharp page/graphics/pagination code.
/// </summary>
public sealed class VoucherPdfDocument
{
    public static readonly XFont TitleFont = new("Arial", 18, XFontStyleEx.Bold);
    public static readonly XFont HeadingFont = new("Arial", 13, XFontStyleEx.Bold);
    public static readonly XFont LabelFont = new("Arial", 9, XFontStyleEx.Regular);
    public static readonly XFont ValueFont = new("Arial", 9, XFontStyleEx.Bold);
    public static readonly XFont HeaderFont = new("Arial", 9, XFontStyleEx.Bold);
    public static readonly XFont CellFont = new("Arial", 9, XFontStyleEx.Regular);
    public static readonly XFont TotalFont = new("Arial", 11, XFontStyleEx.Bold);

    private readonly PdfDocument _document;
    private readonly double _margin;

    private PdfPage _page;
    private XGraphics _gfx;

    public double Y { get; set; }
    public double PageWidth { get; }
    private double PageHeight { get; }

    public VoucherPdfDocument(double margin = 40)
    {
        PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        _document = new PdfDocument();
        _margin = margin;
        _page = _document.AddPage();
        _gfx = XGraphics.FromPdfPage(_page);
        PageWidth = _page.Width.Point - 2 * margin;
        PageHeight = _page.Height.Point;
        Y = margin;
    }

    public void DrawText(string text, XFont font, XBrush brush, double x = 0, double width = 0, XStringFormat? format = null)
    {
        var rect = new XRect(_margin + x, Y, width > 0 ? width : PageWidth - x, 16);
        _gfx.DrawString(text, font, brush, rect, format ?? XStringFormats.TopLeft);
    }

    public void DrawRule()
    {
        _gfx.DrawLine(XPens.Gray, _margin, Y, _margin + PageWidth, Y);
    }

    public void EnsureSpace(double neededHeight, double bottomReserve = 0)
    {
        if (Y + neededHeight > PageHeight - _margin - bottomReserve)
        {
            _page = _document.AddPage();
            _gfx = XGraphics.FromPdfPage(_page);
            Y = _margin;
        }
    }

    /// <summary>Draws a shaded header row for a weighted-column table and returns the column x-offsets.</summary>
    public double[] DrawTableHeader(string[] headers, double[] weights, double rowHeight = 18)
    {
        var offsets = ColumnOffsets(weights);
        EnsureSpace(rowHeight);
        _gfx.DrawRectangle(XBrushes.WhiteSmoke, _margin, Y, PageWidth, rowHeight);
        for (var c = 0; c < headers.Length; c++)
            DrawCell(offsets, c, headers[c], HeaderFont, rightAlign: c > 0, rowHeight);
        _gfx.DrawLine(XPens.Gray, _margin, Y + rowHeight, _margin + PageWidth, Y + rowHeight);
        Y += rowHeight;
        return offsets;
    }

    public void DrawTableRow(double[] columnOffsets, string[] cells, bool[] rightAlign, XFont font, double rowHeight = 18)
    {
        EnsureSpace(rowHeight);
        for (var c = 0; c < cells.Length && c < columnOffsets.Length - 1; c++)
            DrawCell(columnOffsets, c, cells[c], font, rightAlign.Length > c && rightAlign[c], rowHeight);
        _gfx.DrawLine(XPens.LightGray, _margin, Y + rowHeight, _margin + PageWidth, Y + rowHeight);
        Y += rowHeight;
    }

    public double[] ColumnOffsets(double[] weights)
    {
        var totalWeight = weights.Sum();
        var offsets = new double[weights.Length + 1];
        for (var i = 0; i < weights.Length; i++)
            offsets[i + 1] = offsets[i] + PageWidth * weights[i] / totalWeight;
        return offsets;
    }

    private void DrawCell(double[] columnOffsets, int col, string text, XFont font, bool rightAlign, double rowHeight)
    {
        var colWidth = columnOffsets[col + 1] - columnOffsets[col];
        var rect = new XRect(_margin + columnOffsets[col] + 3, Y + 2, colWidth - 6, rowHeight - 4);
        _gfx.Save();
        _gfx.IntersectClip(rect);
        _gfx.DrawString(text, font, XBrushes.Black, rect, rightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft);
        _gfx.Restore();
    }

    public void Save(string filePath) => _document.Save(filePath);
}
