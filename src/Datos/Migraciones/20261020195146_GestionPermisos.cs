using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventario.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class GestionPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudesPermiso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitanteId = table.Column<int>(type: "int", nullable: false),
                    Operacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Justificacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaSolicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AprobadaPorId = table.Column<int>(type: "int", nullable: true),
                    FechaAprobacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RechazadaPorId = table.Column<int>(type: "int", nullable: true),
                    FechaRechazo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAplicacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesPermiso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesPermiso_Usuarios_AprobadaPorId",
                        column: x => x.AprobadaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudesPermiso_Usuarios_RechazadaPorId",
                        column: x => x.RechazadaPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudesPermiso_Usuarios_SolicitanteId",
                        column: x => x.SolicitanteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermisosUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Operacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaOtorgado = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SolicitudPermisoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermisosUsuario_SolicitudesPermiso_SolicitudPermisoId",
                        column: x => x.SolicitudPermisoId,
                        principalTable: "SolicitudesPermiso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermisosUsuario_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermisosUsuario_SolicitudPermisoId",
                table: "PermisosUsuario",
                column: "SolicitudPermisoId");

            migrationBuilder.CreateIndex(
                name: "IX_PermisosUsuario_UsuarioId_Operacion",
                table: "PermisosUsuario",
                columns: new[] { "UsuarioId", "Operacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_AprobadaPorId",
                table: "SolicitudesPermiso",
                column: "AprobadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_Estado",
                table: "SolicitudesPermiso",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_RechazadaPorId",
                table: "SolicitudesPermiso",
                column: "RechazadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPermiso_SolicitanteId",
                table: "SolicitudesPermiso",
                column: "SolicitanteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermisosUsuario");

            migrationBuilder.DropTable(
                name: "SolicitudesPermiso");
        }
    }
}
