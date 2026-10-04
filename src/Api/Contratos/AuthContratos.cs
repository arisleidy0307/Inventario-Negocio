using System.ComponentModel.DataAnnotations;

namespace Inventario.Api.Contratos;

public record RegistroRequest(
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")]
    string Nombre,
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(254)]
    string Correo,
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128)]
    string Contrasena);

public record CorreoRequest(
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(254)]
    string Correo);

public record MensajeResponse(string Mensaje);
