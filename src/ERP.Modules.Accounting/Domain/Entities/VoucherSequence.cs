using ERP.BuildingBlocks.Persistence;
using ERP.BuildingBlocks.Tenancy;

namespace ERP.Modules.Accounting.Domain.Entities;

public class VoucherSequence : Entity, ITenantScoped, ICompanyScoped
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public Guid? PeriodId { get; set; }
    public string SequenceType { get; set; } = "Posted"; // "Draft", "Posted"
    public long CurrentValue { get; set; }
    public string? Prefix { get; set; }
    public string FormatPattern { get; set; } = "{Seq}";
    public uint Version { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }

    public VoucherSequence()
    {
    }

    public VoucherSequence(
        Guid tenantId,
        Guid companyId,
        Guid fiscalYearId,
        string sequenceType,
        Guid? periodId = null,
        string? prefix = null,
        string formatPattern = "{Seq}")
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        PeriodId = periodId;
        SequenceType = sequenceType;
        CurrentValue = 0;
        Prefix = prefix;
        FormatPattern = formatPattern;
        Version = 1;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long NextValue()
    {
        CurrentValue++;
        LastModifiedAtUtc = DateTimeOffset.UtcNow;
        return CurrentValue;
    }
}
