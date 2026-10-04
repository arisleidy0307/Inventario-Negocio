# Plantilla de variables de entorno (PowerShell).
# 1) Copia este archivo:      Copy-Item scripts\variables.ejemplo.ps1 scripts\variables.ps1
# 2) Rellena tus valores en scripts\variables.ps1 (ese archivo está en .gitignore: NUNCA se sube).
# 3) Cárgalo en CADA terminal antes de ejecutar:   . .\scripts\variables.ps1

# Base de datos (SQL Server LocalDB)
$env:ConnectionStrings__Default = "Server=(localdb)\MSSQLLocalDB;Database=InventarioP3;Trusted_Connection=True;TrustServerCertificate=True"

# URL pública de la API: con ella se arman los enlaces de los correos
$env:APP_BASE_URL = "http://localhost:5001"

# Primer Administrador (se crea al arrancar la API si no existe)
$env:ADMIN_EMAIL    = "admin@ejemplo.com"
$env:ADMIN_PASSWORD = "<contraseña-del-admin: 8+ caracteres con letras y números>"

# Servidor SMTP (lo usa solo el EnviadorCorreos)
$env:SMTP_HOST     = "smtp.gmail.com"
$env:SMTP_PORT     = "587"
$env:SMTP_USER     = "<tu-correo@gmail.com>"
$env:SMTP_PASSWORD = "<contraseña-de-aplicación-de-16-letras>"
$env:SMTP_FROM     = "<tu-correo@gmail.com>"

# Opcionales (valores por defecto entre paréntesis)
# $env:ACTIVACION_MINUTOS   = "1440"   # vigencia del enlace de activación (24 h)
# $env:RECUPERACION_MINUTOS = "30"     # vigencia del código de recuperación
# $env:SESION_HORAS         = "8"      # vigencia de la credencial de sesión
# $env:SMTP_SSL             = "true"   # STARTTLS
