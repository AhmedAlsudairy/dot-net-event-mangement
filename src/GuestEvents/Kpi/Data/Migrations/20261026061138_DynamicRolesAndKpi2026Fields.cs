using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpi.Dashboard.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DynamicRolesAndKpi2026Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BranchTarget2026",
                table: "Kpis",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrevSn",
                table: "Kpis",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdateNotes",
                table: "Kpis",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.Permission });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Carry the old enum column over: seed the three system roles with the permissions
            // they had before, then point every existing user at the matching role row.
            migrationBuilder.Sql("INSERT INTO Roles (Name, Description, IsSystem) VALUES ('Admin', 'كل الصلاحيات — لا يمكن تعديل صلاحياته.', 1), ('EntityEditor', 'تحديث بيانات جهته فقط.', 1), ('Viewer', 'اطلاع فقط.', 1);");
            foreach (var perm in new[] { "dashboard.view", "tracking.view", "tracking.edit.own", "tracking.edit.all", "tracking.assign", "kpi.manage", "kpi.delete", "kpi.import", "report.export", "entity.manage", "user.manage", "role.manage" })
                migrationBuilder.Sql($"INSERT INTO RolePermissions (RoleId, Permission) SELECT Id, '{perm}' FROM Roles WHERE Name = 'Admin';");
            foreach (var perm in new[] { "dashboard.view", "tracking.view", "tracking.edit.own", "report.export" })
                migrationBuilder.Sql($"INSERT INTO RolePermissions (RoleId, Permission) SELECT Id, '{perm}' FROM Roles WHERE Name = 'EntityEditor';");
            foreach (var perm in new[] { "dashboard.view", "tracking.view" })
                migrationBuilder.Sql($"INSERT INTO RolePermissions (RoleId, Permission) SELECT Id, '{perm}' FROM Roles WHERE Name = 'Viewer';");
            migrationBuilder.Sql("UPDATE Users SET RoleId = (SELECT Id FROM Roles WHERE Roles.Name = Users.Role);");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BranchTarget2026",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "PrevSn",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "UpdateNotes",
                table: "Kpis");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
