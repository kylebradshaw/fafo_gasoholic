using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gasoholic.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Users, Autos, Fillups, VerificationTokens and their indexes may already exist
            // in prod databases that predate EF migrations. Use IF NOT EXISTS for those.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type = 'U')
                BEGIN
                    CREATE TABLE [Users] (
                        [Id] int NOT NULL IDENTITY(1, 1),
                        [Email] nvarchar(256) NOT NULL,
                        [EmailVerified] bit NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [LastSignIn] datetime2 NULL,
                        [LastInteraction] datetime2 NULL,
                        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Autos]') AND type = 'U')
                BEGIN
                    CREATE TABLE [Autos] (
                        [Id] int NOT NULL IDENTITY(1, 1),
                        [UserId] int NOT NULL,
                        [Brand] nvarchar(max) NOT NULL,
                        [Model] nvarchar(max) NOT NULL,
                        [Plate] nvarchar(max) NOT NULL,
                        [Odometer] decimal(18,2) NOT NULL,
                        CONSTRAINT [PK_Autos] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Autos_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_Autos_UserId] ON [Autos] ([UserId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VerificationTokens]') AND type = 'U')
                BEGIN
                    CREATE TABLE [VerificationTokens] (
                        [Id] int NOT NULL IDENTITY(1, 1),
                        [UserId] int NOT NULL,
                        [Token] nvarchar(450) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [ExpiresAt] datetime2 NOT NULL,
                        [UsedAt] datetime2 NULL,
                        CONSTRAINT [PK_VerificationTokens] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_VerificationTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX [IX_VerificationTokens_Token] ON [VerificationTokens] ([Token]);
                    CREATE INDEX [IX_VerificationTokens_UserId] ON [VerificationTokens] ([UserId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Fillups]') AND type = 'U')
                BEGIN
                    CREATE TABLE [Fillups] (
                        [Id] int NOT NULL IDENTITY(1, 1),
                        [AutoId] int NOT NULL,
                        [FilledAt] datetime2 NOT NULL,
                        [Location] nvarchar(max) NULL,
                        [Latitude] float NULL,
                        [Longitude] float NULL,
                        [FuelType] nvarchar(max) NOT NULL,
                        [PricePerGallon] decimal(18,2) NOT NULL,
                        [Gallons] decimal(18,2) NOT NULL,
                        [Odometer] decimal(18,2) NOT NULL,
                        [IsPartialFill] bit NOT NULL,
                        CONSTRAINT [PK_Fillups] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Fillups_Autos_AutoId] FOREIGN KEY ([AutoId]) REFERENCES [Autos] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_Fillups_AutoId] ON [Fillups] ([AutoId]);
                END
                """);

            // MaintenanceRecords is new — also use IF NOT EXISTS for safety
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MaintenanceRecords]') AND type = 'U')
                BEGIN
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
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fillups");

            migrationBuilder.DropTable(
                name: "MaintenanceRecords");

            migrationBuilder.DropTable(
                name: "VerificationTokens");

            migrationBuilder.DropTable(
                name: "Autos");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
