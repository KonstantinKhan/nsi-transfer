using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCharacterLimitFor_MessageFailure_FailureReasonTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FailureReasonTitle",
                table: "MessageFailures",
                type: "varchar(1024)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(256)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FailureReasonTitle",
                table: "MessageFailures",
                type: "varchar(256)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(1024)");
        }
    }
}
