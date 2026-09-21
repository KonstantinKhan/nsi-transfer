using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageObjectsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageObjects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    PolynomObjectId = table.Column<int>(type: "integer", nullable: false),
                    PolynomTypeId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", nullable: false),
                    SerializedObject = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageObjects_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageObjects_MessageId",
                table: "MessageObjects",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageObjects_Name",
                table: "MessageObjects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MessageObjects_PolynomObjectId_PolynomTypeId",
                table: "MessageObjects",
                columns: new[] { "PolynomObjectId", "PolynomTypeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageObjects");
        }
    }
}
