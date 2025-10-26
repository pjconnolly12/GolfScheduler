using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfScheduler.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRoundRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Rounds_RoundId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_RoundId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "RoundId",
                table: "Players");

            migrationBuilder.CreateTable(
                name: "PlayerRound",
                columns: table => new
                {
                    PlayersId = table.Column<int>(type: "int", nullable: false),
                    RoundsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRound", x => new { x.PlayersId, x.RoundsId });
                    table.ForeignKey(
                        name: "FK_PlayerRound_Players_PlayersId",
                        column: x => x.PlayersId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerRound_Rounds_RoundsId",
                        column: x => x.RoundsId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRound_RoundsId",
                table: "PlayerRound",
                column: "RoundsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerRound");

            migrationBuilder.AddColumn<int>(
                name: "RoundId",
                table: "Players",
                type: "int",
                nullable: true);

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
    }
}
