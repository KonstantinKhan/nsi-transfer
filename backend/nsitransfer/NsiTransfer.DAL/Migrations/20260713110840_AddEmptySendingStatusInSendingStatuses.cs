using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddEmptySendingStatusInSendingStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SendingStatuses",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 4, "Сформировано пустое сообщение. Новых изменений в системе Полином не было найдено", "EmptySending" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SendingStatuses",
                keyColumn: "Id",
                keyValue: 4);
        }
    }
}
