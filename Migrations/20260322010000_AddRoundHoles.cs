using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfScheduler.Migrations
{
    public partial class AddRoundHoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Holes",
                table: "Rounds",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Rounds",
                keyColumn: "Id",
                keyValue: 1,
                column: "Holes",
                value: 18);

            migrationBuilder.UpdateData(
                table: "Rounds",
                keyColumn: "Id",
                keyValue: 2,
                column: "Holes",
                value: 18);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Holes",
                table: "Rounds");
        }
    }
}
