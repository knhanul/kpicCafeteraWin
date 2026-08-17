using ClosedXML.Excel;

namespace KpicCafeteria.Application.DataManagement;

/// <summary>XLSX 워크북 행 단위 읽기.</summary>
public sealed class XlsxWorkbookReader : IDisposable
{
    private readonly XLWorkbook _workbook;

    public XlsxWorkbookReader(string path)
    {
        _workbook = new XLWorkbook(path);
    }

    public IReadOnlyList<string> Sheets
        => _workbook.Worksheets.Select(ws => ws.Name).ToList();

    public bool HasSheet(string name)
        => _workbook.Worksheets.Contains(name);

    public int RowCount(string name)
    {
        var ws = _workbook.Worksheet(name);
        var last = ws.LastRowUsed()?.RowNumber() ?? 0;
        var first = ws.FirstRowUsed()?.RowNumber() ?? 0;
        return Math.Max(0, last - first);
    }

    public IEnumerable<Dictionary<string, object?>> ReadRows(string name)
    {
        var ws = _workbook.Worksheet(name);
        var headerRow = ws.FirstRowUsed()?.RowNumber() ?? 1;
        var headers = ws.Row(headerRow)
            .CellsUsed()
            .Select(c => XlsxCellParser.CleanText(c.Value))
            .ToList();

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;

        for (var rowNum = headerRow + 1; rowNum <= lastRow; rowNum++)
        {
            var row = ws.Row(rowNum);
            var record = new Dictionary<string, object?>();
            for (var i = 0; i < headers.Count; i++)
            {
                var cell = row.Cell(i + 1);
                record[headers[i]] = XlsxCellParser.GetCellValue(cell);
            }

            if (record.Values.Any(v => v is not null and not ""))
            {
                record["__row__"] = rowNum;
                yield return record;
            }
        }
    }

    public void Dispose()
    {
        _workbook.Dispose();
    }
}
