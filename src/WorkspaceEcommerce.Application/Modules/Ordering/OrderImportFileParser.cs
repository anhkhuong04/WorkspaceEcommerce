using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal static class OrderImportFileParser
{
    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["externalordercode"] = "externalOrderCode",
        ["external_order_code"] = "externalOrderCode",
        ["ordercode"] = "externalOrderCode",
        ["order_code"] = "externalOrderCode",
        ["customername"] = "customerName",
        ["customer_name"] = "customerName",
        ["customerphone"] = "customerPhone",
        ["customer_phone"] = "customerPhone",
        ["phone"] = "customerPhone",
        ["customeremail"] = "customerEmail",
        ["customer_email"] = "customerEmail",
        ["email"] = "customerEmail",
        ["shippingaddress"] = "shippingAddress",
        ["shipping_address"] = "shippingAddress",
        ["address"] = "shippingAddress",
        ["shippingstreet"] = "shippingStreet",
        ["shipping_street"] = "shippingStreet",
        ["street"] = "shippingStreet",
        ["shippingward"] = "shippingWard",
        ["shipping_ward"] = "shippingWard",
        ["ward"] = "shippingWard",
        ["shippingprovince"] = "shippingProvince",
        ["shipping_province"] = "shippingProvince",
        ["province"] = "shippingProvince",
        ["paymentmethod"] = "paymentMethod",
        ["payment_method"] = "paymentMethod",
        ["sku"] = "sku",
        ["quantity"] = "quantity",
        ["qty"] = "quantity",
        ["unitprice"] = "unitPrice",
        ["unit_price"] = "unitPrice",
        ["price"] = "unitPrice",
        ["shippingfee"] = "shippingFee",
        ["shipping_fee"] = "shippingFee",
        ["note"] = "note"
    };

    public static async Task<Result<IReadOnlyCollection<AdminOrderImportFileRow>>> ParseAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyCollection<AdminOrderImportFileRow>>.Success(
                await ParseCsvAsync(content, cancellationToken));
        }

        if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyCollection<AdminOrderImportFileRow>>.Success(ParseXlsx(content));
        }

        return Result<IReadOnlyCollection<AdminOrderImportFileRow>>.Validation(["Only CSV and XLSX order import files are supported."]);
    }

    private static async Task<IReadOnlyCollection<AdminOrderImportFileRow>> ParseCsvAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<string[]>();
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                continue;
            }

            rows.Add(ParseCsvLine(line).ToArray());
        }

        return ToFileRows(rows);
    }

    private static IReadOnlyCollection<AdminOrderImportFileRow> ParseXlsx(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        if (sheetEntry is null)
        {
            return Array.Empty<AdminOrderImportFileRow>();
        }

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<string[]>();

        foreach (var row in document.Descendants(ns + "row"))
        {
            var cells = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? "";
                var columnIndex = GetColumnIndex(reference);
                if (columnIndex < 0)
                {
                    continue;
                }

                cells[columnIndex] = ReadCellValue(cell, sharedStrings, ns);
            }

            if (cells.Count == 0)
            {
                continue;
            }

            var values = new string[cells.Keys.Max() + 1];
            foreach (var (index, value) in cells)
            {
                values[index] = value;
            }

            rows.Add(values);
        }

        return ToFileRows(rows);
    }

    private static IReadOnlyCollection<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return Array.Empty<string>();
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        return document
            .Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string ReadCellValue(XElement cell, IReadOnlyCollection<string> sharedStrings, XNamespace ns)
    {
        var type = cell.Attribute("t")?.Value;
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)).Trim();
        }

        var rawValue = cell.Element(ns + "v")?.Value ?? "";
        if (type == "s" &&
            int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex) &&
            sharedStringIndex >= 0 &&
            sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings.ElementAt(sharedStringIndex).Trim();
        }

        return rawValue.Trim();
    }

    private static int GetColumnIndex(string cellReference)
    {
        var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        if (letters.Length == 0)
        {
            return -1;
        }

        var index = 0;
        foreach (var letter in letters.ToUpperInvariant())
        {
            index *= 26;
            index += letter - 'A' + 1;
        }

        return index - 1;
    }

    private static IReadOnlyCollection<AdminOrderImportFileRow> ToFileRows(IReadOnlyCollection<string[]> rows)
    {
        var header = rows.FirstOrDefault();
        if (header is null)
        {
            return Array.Empty<AdminOrderImportFileRow>();
        }

        var headerIndex = header
            .Select((value, index) => new { Header = NormalizeHeader(value), Index = index })
            .Where(item => item.Header is not null)
            .GroupBy(item => item.Header!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

        return rows
            .Skip(1)
            .Select((row, index) => ToFileRow(row, headerIndex, rowNumber: index + 2))
            .Where(row => !IsBlank(row))
            .ToArray();
    }

    private static AdminOrderImportFileRow ToFileRow(
        string[] values,
        IReadOnlyDictionary<string, int> headerIndex,
        int rowNumber)
    {
        return new AdminOrderImportFileRow(
            rowNumber,
            GetValue(values, headerIndex, "externalOrderCode"),
            GetValue(values, headerIndex, "customerName"),
            GetValue(values, headerIndex, "customerPhone"),
            GetValue(values, headerIndex, "customerEmail"),
            GetValue(values, headerIndex, "shippingAddress"),
            GetValue(values, headerIndex, "shippingStreet"),
            GetValue(values, headerIndex, "shippingWard"),
            GetValue(values, headerIndex, "shippingProvince"),
            GetValue(values, headerIndex, "paymentMethod"),
            GetValue(values, headerIndex, "sku"),
            GetValue(values, headerIndex, "quantity"),
            GetValue(values, headerIndex, "unitPrice"),
            GetValue(values, headerIndex, "shippingFee"),
            GetValue(values, headerIndex, "note"));
    }

    private static string GetValue(string[] values, IReadOnlyDictionary<string, int> headerIndex, string key)
    {
        return headerIndex.TryGetValue(key, out var index) && index < values.Length
            ? values[index]?.Trim() ?? ""
            : "";
    }

    private static string? NormalizeHeader(string value)
    {
        var normalized = value.Trim().Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
        return HeaderAliases.TryGetValue(normalized, out var canonical)
            ? canonical
            : null;
    }

    private static bool IsBlank(AdminOrderImportFileRow row)
    {
        return string.IsNullOrWhiteSpace(row.ExternalOrderCode) &&
            string.IsNullOrWhiteSpace(row.CustomerName) &&
            string.IsNullOrWhiteSpace(row.CustomerPhone) &&
            string.IsNullOrWhiteSpace(row.Sku) &&
            string.IsNullOrWhiteSpace(row.Quantity);
    }

    private static IEnumerable<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }
}
