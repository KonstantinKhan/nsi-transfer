using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NsiTransfer.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedCollectionFromPolynomAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    FinishedCollectionFromPolynomAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    SentAtQueue = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    InitiatorName = table.Column<string>(type: "varchar(256)", nullable: false),
                    SerializedMessage = table.Column<string>(type: "jsonb", nullable: false),
                    PublishingResultId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublishingResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", nullable: false),
                    Description = table.Column<string>(type: "varchar(1024)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishingResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolynomObjects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SendingId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolynomObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolynomObjects_Messages_SendingId",
                        column: x => x.SendingId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SendingFailures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FailedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    SerializedFailureDescription = table.Column<string>(type: "jsonb", nullable: false),
                    FailureReasonTitle = table.Column<string>(type: "varchar(256)", nullable: false),
                    SendingId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SendingFailures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SendingFailures_Messages_SendingId",
                        column: x => x.SendingId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PublishingResults",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 0, "Неизвестная ошибка", "Unknown" },
                    { 1, "Подтверждение от RabbitMQ", "Ack" },
                    { 2, "Отрицательное подтверждение от RabbitMQ", "Nack" },
                    { 3, "Превышено время ожидания подтверждения от RabbitMQ", "TimedOut" },
                    { 4, "Подключение к RabbitMQ было заблокировано", "ConnectionBlocked" },
                    { 5, "Не удалось подключиться к RabbitMQ или опубликовать сообщение в очередь RabbitMQ после нескольких попыток", "Failed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolynomObjects_SendingId",
                table: "PolynomObjects",
                column: "SendingId");

            migrationBuilder.CreateIndex(
                name: "IX_SendingFailures_SendingId",
                table: "SendingFailures",
                column: "SendingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolynomObjects");

            migrationBuilder.DropTable(
                name: "PublishingResults");

            migrationBuilder.DropTable(
                name: "SendingFailures");

            migrationBuilder.DropTable(
                name: "Messages");
        }
    }
}
