# Datos ficticios de demostración

`esquema_y_datos.sql` prepara 500 activos ficticios, 12 custodios y 24 actas en una
base SQL Server 2025 cuyo nombre debe contener Demo. No modifica bases de inventario
reales ni crea usuarios o credenciales. Las identidades usan el dominio reservado
example.test y no corresponden al personal de SynerGy.

Conteos esperados de este conjunto, diferentes del inventario empresarial:

| Consulta | Resultado sintético |
|---|---:|
| Laptops totales | 180 |
| Laptops asignadas | 144 |
| Mouses, incluyendo un mouse por kit | 150 |
| Actas activas | 24 |

Crear primero una base vacía llamada SynerGyRagDemo en una instancia autorizada de
SQL Server 2025. Ejecutar el script sobre esa base y el script del catálogo auxiliar
`docs/sql/crear_contexto_vectorial.sql`. Configurar las tres conexiones de la API a
la base demo, indexar catálogo y actas y ejecutar la aplicación según el README.

El script es de inicialización: si las tablas demo ya existen, conserva su contenido.
Si encuentra vistas del sistema de inventario sin tablas demo, se detiene. No incluye
instrucciones DROP, TRUNCATE ni contraseñas.
