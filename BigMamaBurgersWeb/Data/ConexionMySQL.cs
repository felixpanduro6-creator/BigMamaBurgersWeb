using MySqlConnector;
using BigMamaBurgersWeb.Models;
using System.Data;
using System.Text.Json;

namespace BigMamaBurgersWeb.Data
{
    public class ConexionMySQL
    {
        private readonly string? _connectionString;

        public ConexionMySQL(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // 1. LOGIN
        public Usuario? ValidarLogin(string username, string password)
        {
            Usuario? usuario = null;
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = @"SELECT u.id_usuario, u.nombre_completo, u.username, u.id_rol, r.nombre_rol, u.password_hash 
                                 FROM usuarios u INNER JOIN roles r ON u.id_rol = r.id_rol 
                                 WHERE u.username = @user AND u.estado = 'Activo'";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@user", username);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string hashEnBD = reader["password_hash"].ToString() ?? string.Empty;
                            using (var sha256 = System.Security.Cryptography.SHA256.Create())
                            {
                                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                                string hashIngresado = BitConverter.ToString(bytes).Replace("-", "").ToLower();
                                if (hashEnBD == hashIngresado)
                                {
                                    usuario = new Usuario
                                    {
                                        IdUsuario = Convert.ToInt32(reader["id_usuario"]),
                                        NombreCompleto = reader["nombre_completo"].ToString() ?? string.Empty,
                                        Username = reader["username"].ToString() ?? string.Empty,
                                        IdRol = Convert.ToInt32(reader["id_rol"]),
                                        NombreRol = reader["nombre_rol"].ToString() ?? string.Empty
                                    };
                                }
                            }
                        }
                    }
                }
            }
            return usuario;
        }

        // 2. PRODUCTOS (Filtrado por stock disponible)
        public List<Producto> ObtenerProductos()
        {
            var lista = new List<Producto>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = @"
                    SELECT p.id_producto, p.nombre, p.descripcion, p.precio, c.nombre AS categoria, p.imagen_url
                    FROM productos p 
                    INNER JOIN categorias c ON p.id_categoria = c.id_categoria
                    WHERE p.estado = 'Disponible'
                    AND NOT EXISTS (
                        SELECT 1 FROM recetas r 
                        INNER JOIN insumos i ON r.id_insumo = i.id_insumo 
                        WHERE r.id_producto = p.id_producto AND (i.disponible = FALSE OR i.stock_actual <= 0)
                    )";
                using (var command = new MySqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new Producto
                            {
                                IdProducto = Convert.ToInt32(reader["id_producto"]),
                                Nombre = reader["nombre"].ToString() ?? string.Empty,
                                Descripcion = reader["descripcion"].ToString() ?? string.Empty,
                                Precio = Convert.ToDecimal(reader["precio"]),
                                Categoria = reader["categoria"].ToString() ?? string.Empty,
                                ImagenUrl = reader["imagen_url"].ToString() ?? "default.png"
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // 3. CONTROL DE INSUMOS
        public void CambiarEstadoInsumo(int idInsumo, bool disponible)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "UPDATE insumos SET disponible = @disponible WHERE id_insumo = @id";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@disponible", disponible);
                    command.Parameters.AddWithValue("@id", idInsumo);
                    command.ExecuteNonQuery();
                }
            }
        }

        public List<dynamic> ObtenerInsumosControl()
        {
            var lista = new List<dynamic>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT id_insumo, nombre, stock_actual, disponible FROM insumos ORDER BY nombre";
                using (var command = new MySqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new
                            {
                                IdInsumo = Convert.ToInt32(reader["id_insumo"]),
                                Nombre = reader["nombre"].ToString(),
                                Stock = Convert.ToDecimal(reader["stock_actual"]),
                                Disponible = Convert.ToBoolean(reader["disponible"])
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // 4. VENTAS Y PAGO MIXTO
        public bool RegistrarVentaMultipleMixto(int idSucursal, decimal total, decimal montoEfectivo, decimal montoQR, string jsonCarrito)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string metodo = montoEfectivo > 0 && montoQR > 0 ? "Mixto" : (montoEfectivo > 0 ? "Efectivo" : "QR");

                        string queryVenta = @"INSERT INTO ventas (total, metodo_pago, monto_efectivo, monto_qr, id_sucursal, estado) 
                                              VALUES (@total, @metodo, @efectivo, @qr, @sucursal, 'Completada');
                                              SELECT LAST_INSERT_ID();";

                        int idVenta;
                        using (var command = new MySqlCommand(queryVenta, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@total", total);
                            command.Parameters.AddWithValue("@metodo", metodo);
                            command.Parameters.AddWithValue("@efectivo", montoEfectivo);
                            command.Parameters.AddWithValue("@qr", montoQR);
                            command.Parameters.AddWithValue("@sucursal", idSucursal);
                            idVenta = Convert.ToInt32(command.ExecuteScalar());
                        }

                        var productos = JsonSerializer.Deserialize<List<CarritoItem>>(jsonCarrito);
                        if (productos != null)
                        {
                            foreach (var item in productos)
                            {
                                string queryDetalle = @"INSERT INTO detalle_ventas (id_venta, id_producto, cantidad, precio_unitario) 
                                                        VALUES (@idVenta, @idProducto, @cantidad, @precio)";
                                using (var command = new MySqlCommand(queryDetalle, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@idVenta", idVenta);
                                    command.Parameters.AddWithValue("@idProducto", item.id);
                                    command.Parameters.AddWithValue("@cantidad", item.cantidad);
                                    command.Parameters.AddWithValue("@precio", item.precio);
                                    command.ExecuteNonQuery();
                                }

                                // DESCONTAR STOCK
                                string queryStock = @"UPDATE insumos i
                                                      JOIN recetas r ON i.id_insumo = r.id_insumo
                                                      SET i.stock_actual = i.stock_actual - (r.cantidad_requerida * @cantidad)
                                                      WHERE r.id_producto = @idProducto";
                                using (var command = new MySqlCommand(queryStock, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@cantidad", item.cantidad);
                                    command.Parameters.AddWithValue("@idProducto", item.id);
                                    command.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        public class CarritoItem
        {
            public int id { get; set; }
            public string nombre { get; set; } = string.Empty;
            public decimal precio { get; set; }
            public int cantidad { get; set; }
        }

        // 5. VENTAS JAMAICA
        public int ObtenerVentasJamaica()
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT VasosVendidos FROM v_ventas_jamaica";
                using (var command = new MySqlCommand(query, connection))
                {
                    var result = command.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }

        // 6. HISTORIAL, RESUMEN Y GASTOS
        public List<dynamic> ObtenerPedidosAgrupados(int idSucursal)
        {
            var lista = new List<dynamic>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = @"SELECT * FROM v_pedidos_agrupados WHERE DATE(fecha_hora) = CURDATE() ORDER BY fecha_hora DESC";
                using (var command = new MySqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new
                            {
                                IdVenta = reader["id_venta"].ToString(),
                                Hora = Convert.ToDateTime(reader["fecha_hora"]).ToString("HH:mm"),
                                Metodo = reader["metodo_pago"].ToString(),
                                TotalPedido = Convert.ToDecimal(reader["TotalPedido"]),
                                Detalle = reader["DetalleProductos"].ToString()
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public dynamic? ObtenerResumenDia(int idSucursal)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = @"SELECT 
                                    IFNULL(SUM(total), 0) AS TotalDia,
                                    IFNULL(SUM(monto_efectivo), 0) AS TotalEfectivo,
                                    IFNULL(SUM(monto_qr), 0) AS TotalQR
                                 FROM ventas 
                                 WHERE id_sucursal = @sucursal AND DATE(fecha_hora) = CURDATE() AND estado = 'Completada'";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@sucursal", idSucursal);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new
                            {
                                TotalDia = Convert.ToDecimal(reader["TotalDia"]),
                                TotalEfectivo = Convert.ToDecimal(reader["TotalEfectivo"]),
                                TotalQR = Convert.ToDecimal(reader["TotalQR"])
                            };
                        }
                    }
                }
            }
            return null;
        }

        public decimal ObtenerTotalGastos(int idSucursal)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT IFNULL(SUM(monto), 0) FROM gastos WHERE id_sucursal = @suc AND DATE(fecha) = CURDATE()";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@suc", idSucursal);
                    return Convert.ToDecimal(command.ExecuteScalar());
                }
            }
        }

        public string ObtenerQrUrl()
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT qr_pago_url FROM configuracion_negocio LIMIT 1";
                using (var command = new MySqlCommand(query, connection))
                {
                    var result = command.ExecuteScalar();
                    return result != null ? result.ToString()! : "https://via.placeholder.com/300x300?text=Sin+QR";
                }
            }
        }

        public void RegistrarGasto(int idSucursal, string descripcion, decimal monto)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "INSERT INTO gastos (descripcion, monto, id_sucursal) VALUES (@desc, @monto, @suc)";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@desc", descripcion);
                    command.Parameters.AddWithValue("@monto", monto);
                    command.Parameters.AddWithValue("@suc", idSucursal);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void RegistrarFaltante(int idSucursal, string descripcion)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "INSERT INTO faltantes (descripcion, id_sucursal) VALUES (@desc, @suc)";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@desc", descripcion);
                    command.Parameters.AddWithValue("@suc", idSucursal);
                    command.ExecuteNonQuery();
                }
            }
        }

        // =================================================================
        // MÉTODOS PARA EL JEFE Y REPORTES PDF (Los que faltaban)
        // =================================================================

        public List<dynamic> ObtenerReporteDiario()
        {
            var lista = new List<dynamic>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM v_reporte_ventas_diarias";
                using (var command = new MySqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new
                            {
                                Fecha = reader["Fecha"].ToString(),
                                Metodo = reader["Metodo"].ToString(),
                                Sucursal = reader["Sucursal"].ToString(),
                                CantidadPedidos = Convert.ToInt32(reader["Cantidad_Pedidos"]),
                                TotalRecaudado = Convert.ToDecimal(reader["Total_Recaudado"])
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public List<dynamic> ObtenerStockBajo()
        {
            var lista = new List<dynamic>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM v_alerta_stock_bajo";
                using (var command = new MySqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new
                            {
                                Insumo = reader["nombre"].ToString(),
                                StockActual = Convert.ToDecimal(reader["stock_actual"]),
                                StockMinimo = Convert.ToDecimal(reader["stock_minimo"]),
                                Unidad = reader["unidad_medida"].ToString()
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public List<string> ObtenerFaltantes(int idSucursal)
        {
            var lista = new List<string>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT descripcion FROM faltantes WHERE id_sucursal = @suc AND DATE(fecha) = CURDATE()";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@suc", idSucursal);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(reader["descripcion"].ToString() ?? "");
                        }
                    }
                }
            }
            return lista;
        }

        public List<dynamic> ObtenerGastosDetallados(int idSucursal)
        {
            var lista = new List<dynamic>();
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT descripcion, monto FROM gastos WHERE id_sucursal = @suc AND DATE(fecha) = CURDATE()";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@suc", idSucursal);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new
                            {
                                Descripcion = reader["descripcion"].ToString(),
                                Monto = Convert.ToDecimal(reader["monto"])
                            });
                        }
                    }
                }
            }
            return lista;
        }
    }
}