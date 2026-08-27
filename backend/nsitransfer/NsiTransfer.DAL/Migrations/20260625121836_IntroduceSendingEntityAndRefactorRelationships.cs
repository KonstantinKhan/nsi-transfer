using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class IntroduceSendingEntityAndRefactorRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PolynomObjects_Messages_SendingId",
                table: "PolynomObjects");

            migrationBuilder.DropForeignKey(
                name: "FK_SendingFailures_Messages_SendingId",
                table: "SendingFailures");

            migrationBuilder.DropIndex(
                name: "IX_PolynomObjects_SendingId",
                table: "PolynomObjects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SendingFailures",
                table: "SendingFailures");

            migrationBuilder.DropColumn(
                name: "InitiatorName",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SerializedFailureDescription",
                table: "SendingFailures");

            migrationBuilder.RenameTable(
                name: "SendingFailures",
                newName: "MessageFailures");

            migrationBuilder.RenameColumn(
                name: "SendingId",
                table: "PolynomObjects",
                newName: "PolynomObjectId");

            migrationBuilder.RenameColumn(
                name: "SendingId",
                table: "MessageFailures",
                newName: "MessageId");

            migrationBuilder.RenameIndex(
                name: "IX_SendingFailures_SendingId",
                table: "MessageFailures",
                newName: "IX_MessageFailures_MessageId");

            migrationBuilder.AddColumn<long>(
                name: "MessageId",
                table: "PolynomObjects",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<string>(
                name: "SerializedMessage",
                table: "Messages",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SentAtQueue",
                table: "Messages",
                type: "timestamp(3) with time zone",
                precision: 3,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp(3) with time zone",
                oldPrecision: 3);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FinishedCollectionFromPolynomAt",
                table: "Messages",
                type: "timestamp(3) with time zone",
                precision: 3,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp(3) with time zone",
                oldPrecision: 3);

            migrationBuilder.AddColumn<string>(
                name: "FailureDescription",
                table: "MessageFailures",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MessageFailures",
                table: "MessageFailures",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Sendings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    InitiatorName = table.Column<string>(type: "varchar(256)", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sendings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SendingStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", nullable: false),
                    Description = table.Column<string>(type: "varchar(1024)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SendingStatuses", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PublishingResults",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 6, "Не удалось собрать объекты со свойствами перед формированием самого сообщения для отправки", "FailedDuringInformationCollection" });

            migrationBuilder.InsertData(
                table: "SendingStatuses",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 0, "Неизвестный статус, отсутствие статуса", "Unknown" },
                    { 1, "Инициировано начало отправления", "Initiated" },
                    { 2, "В процессе сборки и отправления сообщений", "Pending" },
                    { 3, "Все сообщения из отправления успешно собраны и отправлены в брокер сообщений", "Completed" },
                    { 100, "Неизвестная ошибка", "ErrorUnknown" },
                    { 101, "Ошибка при подготовке отправления", "ErrorOccuredWhilePreparing" },
                    { 102, "Ошибка при сборе данных для одного из сообщений в отправлении", "ErrorOccuredWhileCollectingDataForMessage" },
                    { 103, "Ошибка при публикации одного из сообщений в отправлении", "ErrorOccuredWhilePublishingMessage" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolynomObjects_MessageId",
                table: "PolynomObjects",
                column: "MessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageFailures_Messages_MessageId",
                table: "MessageFailures",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PolynomObjects_Messages_MessageId",
                table: "PolynomObjects",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageFailures_Messages_MessageId",
                table: "MessageFailures");

            migrationBuilder.DropForeignKey(
                name: "FK_PolynomObjects_Messages_MessageId",
                table: "PolynomObjects");

            migrationBuilder.DropTable(
                name: "Sendings");

            migrationBuilder.DropTable(
                name: "SendingStatuses");

            migrationBuilder.DropIndex(
                name: "IX_PolynomObjects_MessageId",
                table: "PolynomObjects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MessageFailures",
                table: "MessageFailures");

            migrationBuilder.DeleteData(
                table: "PublishingResults",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "PolynomObjects");

            migrationBuilder.DropColumn(
                name: "FailureDescription",
                table: "MessageFailures");

            migrationBuilder.RenameTable(
                name: "MessageFailures",
                newName: "SendingFailures");

            migrationBuilder.RenameColumn(
                name: "PolynomObjectId",
                table: "PolynomObjects",
                newName: "SendingId");

            migrationBuilder.RenameColumn(
                name: "MessageId",
                table: "SendingFailures",
                newName: "SendingId");

            migrationBuilder.RenameIndex(
                name: "IX_MessageFailures_MessageId",
                table: "SendingFailures",
                newName: "IX_SendingFailures_SendingId");

            migrationBuilder.AlterColumn<string>(
                name: "SerializedMessage",
                table: "Messages",
                type: "jsonb",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "SentAtQueue",
                table: "Messages",
                type: "timestamp(3) with time zone",
                precision: 3,
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp(3) with time zone",
                oldPrecision: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FinishedCollectionFromPolynomAt",
                table: "Messages",
                type: "timestamp(3) with time zone",
                precision: 3,
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp(3) with time zone",
                oldPrecision: 3,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitiatorName",
                table: "Messages",
                type: "varchar(256)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SerializedFailureDescription",
                table: "SendingFailures",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SendingFailures",
                table: "SendingFailures",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PolynomObjects_SendingId",
                table: "PolynomObjects",
                column: "SendingId");

            migrationBuilder.AddForeignKey(
                name: "FK_PolynomObjects_Messages_SendingId",
                table: "PolynomObjects",
                column: "SendingId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SendingFailures_Messages_SendingId",
                table: "SendingFailures",
                column: "SendingId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
