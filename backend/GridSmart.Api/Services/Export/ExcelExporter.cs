using ClosedXML.Excel;

namespace GridSmart.Api.Services.Export;

/// <summary>Xuat bao cao Excel bang ClosedXML (BE-28).</summary>
public sealed class ExcelExporter : IExcelExporter
{
    public byte[] Export(string sheetName, IReadOnlyList<string> headers, IEnumerable<object?[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName.Length > 31 ? sheetName[..31] : sheetName);
        for (var c = 0; c < headers.Count; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                switch (row[c])
                {
                    case null: break;
                    case DateTime d: cell.Value = d; cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm"; break;
                    case bool b: cell.Value = b ? "Có" : "Không"; break;
                    case int i: cell.Value = i; break;
                    case long l: cell.Value = l; break;
                    case double x: cell.Value = x; break;
                    case decimal m: cell.Value = m; break;
                    default: cell.Value = row[c]!.ToString(); break;
                }
            }
            r++;
        }
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
