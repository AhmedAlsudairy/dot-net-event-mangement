using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpi.Dashboard.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepartmentRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Roles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_EntityId",
                table: "Roles",
                column: "EntityId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Entities_EntityId",
                table: "Roles",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Entities_EntityId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_EntityId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Roles");
        }
    }
}
