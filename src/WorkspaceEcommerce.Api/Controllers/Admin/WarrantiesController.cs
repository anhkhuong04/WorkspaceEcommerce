using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Warranties;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
public sealed class WarrantiesController(IAdminWarrantyService warrantyService) : ControllerBase
{
    [HttpGet("warranty-plans")]
    public async Task<IActionResult> GetPlans([FromQuery] AdminWarrantyPlanListRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.GetPlansAsync(request, cancellationToken));

    [HttpPost("warranty-plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateWarrantyPlanRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.CreatePlanAsync(request, GetActorId(), cancellationToken), StatusCodes.Status201Created);

    [HttpPost("warranty-plans/{id:guid}/retire")]
    public async Task<IActionResult> RetirePlan(Guid id, CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.RetirePlanAsync(id, GetActorId(), cancellationToken));

    [HttpPut("product-variants/{variantId:guid}/warranty-plan")]
    public async Task<IActionResult> AssignPlanToVariant(
        Guid variantId,
        [FromBody] AssignWarrantyPlanRequest request,
        CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.AssignPlanToVariantAsync(variantId, request, GetActorId(), cancellationToken));

    [HttpGet("warranty-units")]
    public async Task<IActionResult> GetUnits([FromQuery] AdminWarrantyUnitListRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.GetUnitsAsync(request, cancellationToken));

    [HttpPost("warranty-units/imports/preview")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> PreviewUnitImport([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var parsed = await ParseCsvAsync(file, cancellationToken);
        if (parsed.Errors.Count > 0)
        {
            return this.ToApiResponse(Result<AdminWarrantyImportResultDto>.Validation(parsed.Errors));
        }

        return this.ToApiResponse(await warrantyService.ImportUnitsAsync(
            new ImportWarrantyUnitsRequest { DryRun = true, Rows = parsed.Rows.ToArray() },
            GetActorId(),
            cancellationToken));
    }

    [HttpPost("warranty-units/imports")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> ImportUnits([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var parsed = await ParseCsvAsync(file, cancellationToken);
        if (parsed.Errors.Count > 0)
        {
            return this.ToApiResponse(Result<AdminWarrantyImportResultDto>.Validation(parsed.Errors), StatusCodes.Status201Created);
        }

        return this.ToApiResponse(await warrantyService.ImportUnitsAsync(
            new ImportWarrantyUnitsRequest { DryRun = false, Rows = parsed.Rows.ToArray() },
            GetActorId(),
            cancellationToken), StatusCodes.Status201Created);
    }

    [HttpPost("warranty-units/{id:guid}/assign")]
    public async Task<IActionResult> AssignUnit(
        Guid id,
        [FromBody] AssignWarrantyUnitRequest request,
        CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.AssignUnitAsync(id, request, GetActorId(), cancellationToken));

    [HttpGet("warranties")]
    public async Task<IActionResult> GetWarranties([FromQuery] AdminWarrantyEntitlementListRequest request, CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.GetEntitlementsAsync(request, cancellationToken));

    [HttpGet("warranties/{id:guid}")]
    public async Task<IActionResult> GetWarranty(Guid id, CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.GetEntitlementAsync(id, cancellationToken));

    [HttpPost("warranties/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.ActivateAsync(id, GetActorId(), cancellationToken));

    [HttpPost("warranties/{id:guid}/void")]
    public async Task<IActionResult> Void(
        Guid id,
        [FromBody] AdminWarrantyReasonRequest request,
        CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.VoidAsync(id, request, GetActorId(), cancellationToken));

    [HttpPost("warranties/{id:guid}/replace")]
    public async Task<IActionResult> Replace(
        Guid id,
        [FromBody] ReplaceWarrantyRequest request,
        CancellationToken cancellationToken) =>
        this.ToApiResponse(await warrantyService.ReplaceAsync(id, request, GetActorId(), cancellationToken));

    private string GetActorId() => User.FindFirstValue(ClaimTypes.Name) ??
        User.FindFirstValue(ClaimTypes.Email) ??
        User.Identity?.Name ??
        "admin";

    private static async Task<ParsedCsv> ParseCsvAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ParsedCsv.Fail("Warranty import file is required.");
        }

        if (file.Length > 2 * 1024 * 1024)
        {
            return ParsedCsv.Fail("Warranty import file exceeds the 2 MB limit.");
        }

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return ParsedCsv.Fail("Warranty import file must be a CSV file.");
        }

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (headerLine is null)
        {
            return ParsedCsv.Fail("Warranty import CSV is empty.");
        }

        var headers = ParseCsvLine(headerLine).Select(header => header.Trim().ToLowerInvariant()).ToArray();
        var skuIndex = Array.IndexOf(headers, "sku");
        var identifierIndex = Array.FindIndex(headers, header => header is "identifier" or "serial" or "imei");
        var typeIndex = Array.FindIndex(headers, header => header is "identifier_type" or "type");
        if (skuIndex < 0 || identifierIndex < 0)
        {
            return ParsedCsv.Fail("Warranty import CSV must include sku and identifier columns.");
        }

        var rows = new List<WarrantyUnitImportRow>();
        var physicalLine = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            physicalLine++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (rows.Count >= 10_000)
            {
                return ParsedCsv.Fail("Warranty import CSV exceeds 10,000 data rows.");
            }

            var fields = ParseCsvLine(line);
            var identifierTypeText = GetField(fields, typeIndex);
            var identifierType = ParseIdentifierType(identifierTypeText);
            rows.Add(new WarrantyUnitImportRow
            {
                RowNumber = physicalLine,
                Sku = GetField(fields, skuIndex),
                Identifier = GetField(fields, identifierIndex),
                IdentifierType = identifierType,
                HasInvalidIdentifierType = !string.IsNullOrWhiteSpace(identifierTypeText) && identifierType is null
            });
        }

        return new ParsedCsv(rows, []);
    }

    private static string GetField(IReadOnlyList<string> fields, int index) => index >= 0 && index < fields.Count ? fields[index].Trim() : string.Empty;

    private static WarrantyIdentifierType? ParseIdentifierType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "" => null,
        "serial" => WarrantyIdentifierType.Serial,
        "imei" => WarrantyIdentifierType.Imei,
        _ => null
    };

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append(character);
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString());
        return values;
    }

    private sealed record ParsedCsv(IReadOnlyList<WarrantyUnitImportRow> Rows, IReadOnlyCollection<string> Errors)
    {
        public static ParsedCsv Fail(string error) => new([], [error]);
    }
}
