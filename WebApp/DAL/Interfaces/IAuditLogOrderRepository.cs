using DefaultNamespace.models;

namespace DefaultNamespace.Interfaces;

public interface IAuditLogOrderRepository
{
    Task<V1AuditLogOrderDal[]> BulkInsert(V1AuditLogOrderDal[] logs, CancellationToken token);
}
