using Models.Dto.Common;

namespace Models.Dto.V1.Responses;

public class V1CreateAuditLogResponse
{
    public AuditLogOrderUnit[] Orders { get; set; }
}