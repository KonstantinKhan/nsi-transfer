using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddClassificationGroupCodeMaxTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassificationGroupCodeMaxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupObjectId = table.Column<int>(type: "integer", nullable: false),
                    GroupTypeId = table.Column<int>(type: "integer", nullable: false),
                    LastMaxCode = table.Column<string>(type: "varchar(64)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationGroupCodeMaxes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationGroupCodeMaxes_GroupObjectId_GroupTypeId",
                table: "ClassificationGroupCodeMaxes",
                columns: new[] { "GroupObjectId", "GroupTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassificationGroupCodeMaxes");
        }
    }
}
