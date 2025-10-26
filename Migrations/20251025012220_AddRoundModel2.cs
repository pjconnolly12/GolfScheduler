using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfScheduler.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundModel2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoundId",
                table: "Players",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_RoundId",
                table: "Players",
                column: "RoundId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Rounds_RoundId",
                table: "Players",
                column: "RoundId",
                principalTable: "Rounds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Rounds_RoundId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropIndex(
                name: "IX_Players_RoundId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "RoundId",
                table: "Players");
        }
    }
}
