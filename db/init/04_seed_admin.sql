-- =====================================================================
-- Usuario administrador inicial
-- =====================================================================
-- Usuario:     admin
-- Contraseña:  Admin123!
-- Hash BCrypt (workFactor 12) generado y verificado con BCrypt.Net-Next.
--
-- IMPORTANTE: cambia esta contraseña después del primer inicio de sesión.
--             No uses estas credenciales en producción.
--
-- Idempotente: no crea el usuario si ya existe (por nombre 'admin').
-- =====================================================================

INSERT INTO Usuarios (IdRol, Usuario, PasswordHash, NombreCompleto, Estatus)
SELECT
    1,                                  -- Rol Administrador (ver 02_seed.sql)
    'admin',
    '$2a$12$/GEzz08X5VvfT31OqmXBlOO4YmWOsiiyXV.Dy99Mx7UhILJrh3DQ6',
    'Administrador del Sistema',
    1
    WHERE NOT EXISTS (
    SELECT 1 FROM Usuarios WHERE Usuario = 'admin'
);
