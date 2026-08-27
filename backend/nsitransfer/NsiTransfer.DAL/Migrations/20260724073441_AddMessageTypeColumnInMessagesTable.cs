using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageTypeColumnInMessagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MessageType",
                table: "Messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "Messages");
        }
    }
}
