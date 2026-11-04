using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Stena.History.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cursors",
                columns: table => new
                {
                    WallId = table.Column<int>(type: "integer", nullable: false),
                    Ms = table.Column<long>(type: "bigint", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursors", x => x.WallId);
                });

            migrationBuilder.CreateTable(
                name: "Pixels",
                columns: table => new
                {
                    WallId = table.Column<int>(type: "integer", nullable: false),
                    Ms = table.Column<long>(type: "bigint", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<byte>(type: "smallint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pixels", x => new { x.WallId, x.Ms, x.Seq });
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WallId = table.Column<int>(type: "integer", nullable: false),
                    Ms = table.Column<long>(type: "bigint", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Pixels = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pixels_WallId_X_Y_Ms",
                table: "Pixels",
                columns: new[] { "WallId", "X", "Y", "Ms" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_WallId_Ms",
                table: "Snapshots",
                columns: new[] { "WallId", "Ms" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cursors");

            migrationBuilder.DropTable(
                name: "Pixels");

            migrationBuilder.DropTable(
                name: "Snapshots");
        }
    }
}
