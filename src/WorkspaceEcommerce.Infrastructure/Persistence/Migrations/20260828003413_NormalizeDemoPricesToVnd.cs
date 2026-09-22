using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkspaceEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeDemoPricesToVnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE catalog.product_variants AS variant
                SET price = converted.price,
                    compare_at_price = converted.compare_at_price
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000001'::uuid, 18174000::numeric, 20774000::numeric),
                    ('30000000-0000-0000-0000-000000000002'::uuid, 19474000::numeric, 22074000::numeric),
                    ('30000000-0000-0000-0000-000000000003'::uuid,  8554000::numeric, 10374000::numeric),
                    ('30000000-0000-0000-0000-000000000004'::uuid,  4914000::numeric,  5954000::numeric),
                    ('30000000-0000-0000-0000-000000000005'::uuid,  2054000::numeric,  2574000::numeric)
                ) AS converted(id, price, compare_at_price)
                WHERE variant.id = converted.id;

                UPDATE cart.cart_items AS item
                SET unit_price_snapshot = converted.unit_price
                FROM (VALUES
                    ('71000000-0000-0000-0000-000000000001'::uuid, 18174000::numeric),
                    ('71000000-0000-0000-0000-000000000002'::uuid,  2054000::numeric)
                ) AS converted(id, unit_price)
                WHERE item.id = converted.id;

                UPDATE ordering.order_items AS item
                SET unit_price = converted.unit_price
                FROM (VALUES
                    ('81000000-0000-0000-0000-000000000001'::uuid, 18174000::numeric),
                    ('81000000-0000-0000-0000-000000000002'::uuid,  8554000::numeric),
                    ('81000000-0000-0000-0000-000000000003'::uuid,  4914000::numeric),
                    ('81000000-0000-0000-0000-000000000004'::uuid,  2054000::numeric)
                ) AS converted(id, unit_price)
                WHERE item.id = converted.id;

                UPDATE ordering.orders AS orders
                SET subtotal = converted.total,
                    total_amount = converted.total,
                    currency_code = 'VND',
                    exchange_rate = 1
                FROM (VALUES
                    ('80000000-0000-0000-0000-000000000001'::uuid, 18174000::numeric),
                    ('80000000-0000-0000-0000-000000000002'::uuid,  8554000::numeric),
                    ('80000000-0000-0000-0000-000000000003'::uuid, 11882000::numeric)
                ) AS converted(id, total)
                WHERE orders.id = converted.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE catalog.product_variants AS variant
                SET price = converted.price,
                    compare_at_price = converted.compare_at_price
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000001'::uuid, 699::numeric, 799::numeric),
                    ('30000000-0000-0000-0000-000000000002'::uuid, 749::numeric, 849::numeric),
                    ('30000000-0000-0000-0000-000000000003'::uuid, 329::numeric, 399::numeric),
                    ('30000000-0000-0000-0000-000000000004'::uuid, 189::numeric, 229::numeric),
                    ('30000000-0000-0000-0000-000000000005'::uuid,  79::numeric,  99::numeric)
                ) AS converted(id, price, compare_at_price)
                WHERE variant.id = converted.id;

                UPDATE cart.cart_items AS item
                SET unit_price_snapshot = converted.unit_price
                FROM (VALUES
                    ('71000000-0000-0000-0000-000000000001'::uuid, 699::numeric),
                    ('71000000-0000-0000-0000-000000000002'::uuid,  79::numeric)
                ) AS converted(id, unit_price)
                WHERE item.id = converted.id;

                UPDATE ordering.order_items AS item
                SET unit_price = converted.unit_price
                FROM (VALUES
                    ('81000000-0000-0000-0000-000000000001'::uuid, 699::numeric),
                    ('81000000-0000-0000-0000-000000000002'::uuid, 329::numeric),
                    ('81000000-0000-0000-0000-000000000003'::uuid, 189::numeric),
                    ('81000000-0000-0000-0000-000000000004'::uuid,  79::numeric)
                ) AS converted(id, unit_price)
                WHERE item.id = converted.id;

                UPDATE ordering.orders AS orders
                SET subtotal = converted.total,
                    total_amount = converted.total,
                    currency_code = 'USD',
                    exchange_rate = 1
                FROM (VALUES
                    ('80000000-0000-0000-0000-000000000001'::uuid, 699::numeric),
                    ('80000000-0000-0000-0000-000000000002'::uuid, 329::numeric),
                    ('80000000-0000-0000-0000-000000000003'::uuid, 457::numeric)
                ) AS converted(id, total)
                WHERE orders.id = converted.id;
                """);
        }
    }
}
