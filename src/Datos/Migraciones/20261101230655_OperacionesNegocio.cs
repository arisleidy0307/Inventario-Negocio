using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class OperacionesNegocio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCancelacion",
                table: "OrdenesCompra",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecibidaPorUsuarioId",
                table: "OrdenesCompra",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaCancelacion",
                table: "OrdenesCompra");

            migrationBuilder.DropColumn(
                name: "RecibidaPorUsuarioId",
                table: "OrdenesCompra");
        }
    }
}
