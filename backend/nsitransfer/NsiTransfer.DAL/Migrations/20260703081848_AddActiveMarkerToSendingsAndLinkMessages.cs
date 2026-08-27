using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveMarkerToSendingsAndLinkMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndedAt",
                table: "Sendings",
                type: "timestamp(3) with time zone",
                precision: 3,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp(3) with time zone",
                oldPrecision: 3);

            migrationBuilder.AddColumn<bool>(
                name: "ActiveMarker",
                table: "Sendings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SendingId",
                table: "Messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Sendings_ActiveMarker",
                table: "Sendings",
                column: "ActiveMarker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SendingId",
                table: "Messages",
                column: "SendingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Sendings_SendingId",
                table: "Messages",
                column: "SendingId",
                principalTable: "Sendings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Sendings_SendingId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Sendings_ActiveMarker",
                table: "Sendings");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SendingId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ActiveMarker",
                table: "Sendings");

            migrationBuilder.DropColumn(
                name: "SendingId",
                table: "Messages");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndedAt",
                table: "Sendings",
                type: "timestamp(3) with time zone",
                precision: 3,
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp(3) with time zone",
                oldPrecision: 3,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TargetReferenceNodeName = table.Column<string>(type: "varchar(128)", nullable: false),
                    TargetReferenceNodeObjectId = table.Column<int>(type: "integer", nullable: false),
                    TargetReferenceNodeTypeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Id);
                });
        }
    }
}
