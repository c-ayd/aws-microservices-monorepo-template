using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.RabbitMq.Helpers.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class InitialMig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WrappedDataKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Value = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WrappedDataKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DroppedMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "text", nullable: false),
                    PublisherName = table.Column<string>(type: "text", nullable: false),
                    ExchangeName = table.Column<string>(type: "text", nullable: false),
                    RoutingKey = table.Column<string>(type: "text", nullable: false),
                    Properties = table.Column<string>(type: "text", nullable: false),
                    BodyEncrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    WrappedDataKeyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DroppedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DroppedMessages_WrappedDataKeys_WrappedDataKeyId",
                        column: x => x.WrappedDataKeyId,
                        principalTable: "WrappedDataKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DroppedMessages_WrappedDataKeyId",
                table: "DroppedMessages",
                column: "WrappedDataKeyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DroppedMessages");

            migrationBuilder.DropTable(
                name: "WrappedDataKeys");
        }
    }
}
