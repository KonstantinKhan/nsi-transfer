using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnClassificationCodeInPolynomObject_AddTypeIdColumnInPolynomObjects_AddPolynomObjectFailureTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassificationCode",
                table: "PolynomObjects",
                type: "varchar(64)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PolynomTypeId",
                table: "PolynomObjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PolynomObjectFailures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    PolynomObjectId = table.Column<int>(type: "integer", nullable: false),
                    PolynomTypeId = table.Column<int>(type: "integer", nullable: false),
                    ObjectName = table.Column<string>(type: "varchar(256)", nullable: false),
                    FailureType = table.Column<string>(type: "text", nullable: false),
                    FailedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolynomObjectFailures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolynomObjectFailures_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolynomObjects_ClassificationCode",
                table: "PolynomObjects",
                column: "ClassificationCode");

            migrationBuilder.CreateIndex(
                name: "IX_PolynomObjects_Name",
                table: "PolynomObjects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PolynomObjectFailures_MessageId",
                table: "PolynomObjectFailures",
                column: "MessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolynomObjectFailures");

            migrationBuilder.DropIndex(
                name: "IX_PolynomObjects_ClassificationCode",
                table: "PolynomObjects");

            migrationBuilder.DropIndex(
                name: "IX_PolynomObjects_Name",
                table: "PolynomObjects");

            migrationBuilder.DropColumn(
                name: "ClassificationCode",
                table: "PolynomObjects");

            migrationBuilder.DropColumn(
                name: "PolynomTypeId",
                table: "PolynomObjects");
        }
    }
}
