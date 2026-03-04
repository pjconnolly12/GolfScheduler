using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfScheduler.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDistributionList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistributionListMembers",
                columns: table => new
                {
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberUserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionListMembers", x => new { x.OwnerUserId, x.MemberUserId });
                    table.ForeignKey(
                        name: "FK_DistributionListMembers_AspNetUsers_MemberUserId",
                        column: x => x.MemberUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DistributionListMembers_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionListMembers_MemberUserId",
                table: "DistributionListMembers",
                column: "MemberUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributionListMembers");
        }
    }
}
