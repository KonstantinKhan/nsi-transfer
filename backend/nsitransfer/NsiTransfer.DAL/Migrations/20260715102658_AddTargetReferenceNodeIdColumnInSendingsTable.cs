using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetReferenceNodeIdColumnInSendingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetReferenceNodeId",
                table: "Sendings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TargetReferenceNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObjectId = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetReferenceNodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sendings_TargetReferenceNodeId",
                table: "Sendings",
                column: "TargetReferenceNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sendings_TargetReferenceNodes_TargetReferenceNodeId",
                table: "Sendings",
                column: "TargetReferenceNodeId",
                principalTable: "TargetReferenceNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sendings_TargetReferenceNodes_TargetReferenceNodeId",
                table: "Sendings");

            migrationBuilder.DropTable(
                name: "TargetReferenceNodes");

            migrationBuilder.DropIndex(
                name: "IX_Sendings_TargetReferenceNodeId",
                table: "Sendings");

            migrationBuilder.DropColumn(
                name: "TargetReferenceNodeId",
                table: "Sendings");
        }
    }
}
