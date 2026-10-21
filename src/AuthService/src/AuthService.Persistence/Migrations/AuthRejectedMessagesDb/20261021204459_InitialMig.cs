using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Persistence.Migrations.AuthRejectedMessagesDb
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
                    Value = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WrappedDataKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RejectedMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublisherName = table.Column<string>(type: "text", nullable: false),
                    ExchangeName = table.Column<string>(type: "text", nullable: false),
                    RoutingKey = table.Column<string>(type: "text", nullable: false),
                    Properties = table.Column<string>(type: "text", nullable: false),
                    BodyEncrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    WrappedDataKeyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RejectedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RejectedMessages_WrappedDataKeys_WrappedDataKeyId",
                        column: x => x.WrappedDataKeyId,
                        principalTable: "WrappedDataKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RejectedMessages_WrappedDataKeyId",
                table: "RejectedMessages",
                column: "WrappedDataKeyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RejectedMessages");

            migrationBuilder.DropTable(
                name: "WrappedDataKeys");
        }
    }
}
