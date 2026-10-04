using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class DesactivacionUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Deshabilitado",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deshabilitado",
                table: "Usuarios");
        }
    }
}
