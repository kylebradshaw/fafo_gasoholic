using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gasoholic.Migrations
{
    /// <inheritdoc />
    public partial class FixMaintenanceColumnTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS [dbo].[MaintenanceRecords];

                CREATE TABLE [MaintenanceRecords] (
                    [Id] int NOT NULL IDENTITY(1, 1),
                    [AutoId] int NOT NULL,
                    [Type] nvarchar(max) NOT NULL,
                    [PerformedAt] datetime2 NOT NULL,
                    [Odometer] decimal(18,2) NOT NULL,
                    [Cost] decimal(18,2) NOT NULL,
                    [Notes] nvarchar(max) NULL,
                    CONSTRAINT [PK_MaintenanceRecords] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_MaintenanceRecords_Autos_AutoId] FOREIGN KEY ([AutoId]) REFERENCES [Autos] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_MaintenanceRecords_AutoId] ON [MaintenanceRecords] ([AutoId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[MaintenanceRecords]");
        }
    }
}
