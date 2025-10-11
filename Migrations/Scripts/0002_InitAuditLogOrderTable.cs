using FluentMigrator;

namespace Migrations.Scripts;

[Migration(2)]
public class InitAuditLogOrderTable : Migration
{
    public override void Up()
    {
        var sql = @"
            create table if not exists audit_log_order (
                id bigserial not null primary key,
                order_id bigint not null,
                order_item_id bigint not null,
                customer_id bigint not null,
                order_status text not null,
                created_at timestamp with time zone not null,
                updated_at timestamp with time zone not null
            );

            create index if not exists idx_audit_log_order_order_id on audit_log_order(order_id);
            create index if not exists idx_audit_log_order_customer_id on audit_log_order(customer_id);

            create type v1_audit_log_order as (
                id bigint,
                order_id bigint,
                order_item_id bigint,
                customer_id bigint,
                order_status text,
                created_at timestamp with time zone,
                updated_at timestamp with time zone
            );
        ";

        Execute.Sql(sql);
    }

    public override void Down()
    {
        throw new NotImplementedException();
    }
}