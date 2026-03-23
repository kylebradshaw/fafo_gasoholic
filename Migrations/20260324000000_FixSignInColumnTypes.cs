using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gasoholic.Migrations
{
    /// <summary>
    /// Fixes LastSignIn and LastInteraction column types on SQL Server.
    /// The AddSignInTracking migration was generated against SQLite, which uses "TEXT" for
    /// DateTime columns. On SQL Server, "text" is a deprecated LOB type that is incompatible
    /// with datetime2 parameters, causing "Operand type clash: datetime2 is incompatible with text".
    /// This migration drops and recreates the columns with the correct type for each provider.
    /// </summary>
    public partial class FixSignInColumnTypes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // On SQL Server, the columns were created as 'text' (deprecated LOB) instead of datetime2.
            // On SQLite, 'TEXT' is correct for DateTime — this is a no-op.
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql(@"
                    IF EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LastSignIn' AND DATA_TYPE = 'text'
                    )
                    BEGIN
                        ALTER TABLE Users DROP COLUMN LastSignIn;
                        ALTER TABLE Users ADD LastSignIn datetime2(7) NULL;
                    END
                ");
                migrationBuilder.Sql(@"
                    IF EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'LastInteraction' AND DATA_TYPE = 'text'
                    )
                    BEGIN
                        ALTER TABLE Users DROP COLUMN LastInteraction;
                        ALTER TABLE Users ADD LastInteraction datetime2(7) NULL;
                    END
                ");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: reverting would re-introduce the bug
        }
    }
}
