using App.Shared.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <summary>
    /// Search support for <c>GET /api/v1/search</c>: the pg_trgm extension and GIN trigram indexes on
    /// the inventory columns the global search matches with ILIKE.
    ///
    /// Hand-written, raw and idempotent SQL: the entity model does not change, so there is no
    /// designer file and the model snapshot is untouched. The search works without this
    /// migration (sequential ILIKE scans, which are faster than any index below roughly ten
    /// thousand rows); it matters once the inventory grows and for typo matching on large tables.
    /// pg_trgm is a trusted extension: the migration role needs CREATE on the database, not superuser.
    /// On a database that already holds a very large inventory, create these indexes by hand with
    /// CREATE INDEX CONCURRENTLY before applying the migration, so writes are not blocked.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261111103000_SearchTrigramIndexes")]
    public class SearchTrigramIndexes : Migration
    {
        private static readonly (string Index, string Column)[] InventoryIndexes =
        {
            ("ix_inventory_items_name_trgm", "name"),
            ("ix_inventory_items_display_name_trgm", "display_name"),
            ("ix_inventory_items_serial_number_trgm", "serial_number"),
            ("ix_inventory_items_storage_location_trgm", "storage_location")
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pinned to "public" so the operator class resolves the same way for every role.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;");

            foreach (var (index, column) in InventoryIndexes)
            {
                migrationBuilder.Sql(
                    $"CREATE INDEX IF NOT EXISTS {index} ON backend.inventory_items USING gin ({column} public.gin_trgm_ops);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (index, _) in InventoryIndexes)
            {
                migrationBuilder.Sql($"DROP INDEX IF EXISTS backend.{index};");
            }
            // The extension is left installed: other objects may depend on it by then.
        }
    }
}
