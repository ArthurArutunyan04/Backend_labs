using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.Dto.V1.Responses
{
    public class V1AuditLogOrderResponse
    {
        public AuditLogOrder[] AuditLogs { get; set; } = Array.Empty<AuditLogOrder>();

        public class AuditLogOrder
        {
            public long Id { get; set; }
            public long OrderId { get; set; }
            public long OrderItemId { get; set; }
            public long CustomerId { get; set; }
            public string OrderStatus { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset UpdatedAt { get; set; }
        }
    }
}
