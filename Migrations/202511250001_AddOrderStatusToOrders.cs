using FluentMigrator;
using FluentMigrator.Infrastructure.Extensions;

namespace WebApp.Migrations
{
    [Migration(202511250001)]
    public class AddOrderStatusToOrders : Migration
    {
        public override void Up()
        {
            if (!Schema.Table("orders").Column("order_status").Exists())
            {
                Alter.Table("orders")
                    .AddColumn("order_status").AsString().NotNullable().WithDefaultValue("Created");

                Execute.Sql("UPDATE orders SET order_status = 'Created' WHERE order_status IS NULL;");
            }

            Execute.Sql("ALTER TYPE v1_order ADD ATTRIBUTE order_status text;");
        }

        public override void Down()
        {
            Execute.Sql("ALTER TYPE v1_order DROP ATTRIBUTE order_status;");
            if (Schema.Table("orders").Column("order_status").Exists())
            {
                Delete.Column("order_status").FromTable("orders");
            }
        }
    }
}

