using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gasoholic.Migrations
{
    /// <inheritdoc />
    public partial class CreateSessionCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SessionCache]') AND type = 'U')
                BEGIN
                    CREATE TABLE [dbo].[SessionCache] (
                        [Id]                         NVARCHAR(449)     NOT NULL,
                        [Value]                      VARBINARY(MAX)    NOT NULL,
                        [ExpiresAtTime]              DATETIMEOFFSET    NOT NULL,
                        [SlidingExpirationInSeconds] BIGINT            NULL,
                        [AbsoluteExpiration]         DATETIMEOFFSET    NULL,
                        PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_SessionCache_ExpiresAtTime] ON [dbo].[SessionCache] ([ExpiresAtTime]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[SessionCache]");
        }
    }
}
