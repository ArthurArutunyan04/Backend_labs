using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FluentMigrator;

namespace WebApp.Migrations
{
    [Migration(202501240000)]
    public class CreateAuditLogOrderTable : Migration
    {
        public override void Up()
        {
            Create.Table("audit_log_order")
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("order_id").AsInt64().NotNullable()
                .WithColumn("order_item_id").AsInt64().NotNullable()
                .WithColumn("customer_id").AsInt64().NotNullable()
                .WithColumn("order_status").AsString().NotNullable()
                .WithColumn("created_at").AsDateTimeOffset().NotNullable()
                .WithColumn("updated_at").AsDateTimeOffset().NotNullable();
        }

        public override void Down()
        {
            Delete.Table("audit_log_order");
        }
    }
}
