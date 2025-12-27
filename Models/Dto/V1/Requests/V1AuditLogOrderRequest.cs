using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.Dto.V1.Requests
{
    public class V1AuditLogOrderRequest
    {
        public LogOrder[] Orders { get; set; } = Array.Empty<LogOrder>();

        public class LogOrder
        {
            public long OrderId { get; set; }
            public long OrderItemId { get; set; }
            public long CustomerId { get; set; }
            public string OrderStatus { get; set; } = string.Empty;
        }
    }
}
