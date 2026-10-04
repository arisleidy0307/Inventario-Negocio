using System.ComponentModel.DataAnnotations;

namespace Inventario.Api.Contratos;

public record CambiarRolRequest(
    [Required(ErrorMessage = "El rol es obligatorio.")]
    [StringLength(20)]
    string Rol);
