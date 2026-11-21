using Microsoft.AspNetCore.Mvc;
using BigMamaBurgersWeb.Data;

namespace BigMamaBurgersWeb.Controllers
{
    public class JuandelaRosaController : Controller
    {
        private readonly ConexionMySQL _conexion;

        public JuandelaRosaController(ConexionMySQL conexion)
        {
            _conexion = conexion;
        }

        public IActionResult Index()
        {
            ViewBag.Productos = _conexion.ObtenerProductos();
            ViewBag.MisVentas = _conexion.ObtenerPedidosAgrupados(1);
            ViewBag.Resumen = _conexion.ObtenerResumenDia(1);
            ViewBag.GastosTotales = _conexion.ObtenerTotalGastos(1);
            ViewBag.QrUrl = _conexion.ObtenerQrUrl();
            ViewBag.VentasJamaica = _conexion.ObtenerVentasJamaica();
            ViewBag.InsumosControl = _conexion.ObtenerInsumosControl();

            return View();
        }

        [HttpPost]
        public IActionResult RegistrarCarrito(string metodoPago, string carritoJson, decimal total, decimal montoEfectivo, decimal montoQR)
        {
            if (!string.IsNullOrEmpty(carritoJson) && total > 0)
            {
                bool exito = _conexion.RegistrarVentaMultipleMixto(1, total, montoEfectivo, montoQR, carritoJson);

                if (exito) TempData["Mensaje"] = "¡Pedido registrado con éxito!";
                else TempData["Error"] = "Error al procesar el pedido (posible falta de stock).";
            }
            else
            {
                TempData["Error"] = "El carrito está vacío.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult CambiarEstadoInsumo(int idInsumo, bool disponible)
        {
            _conexion.CambiarEstadoInsumo(idInsumo, disponible);
            TempData["Mensaje"] = disponible ? "Insumo marcado como Disponible." : "Insumo marcado como Agotado.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RegistrarGasto(string descripcion, decimal monto)
        {
            _conexion.RegistrarGasto(1, descripcion, monto);
            TempData["Mensaje"] = $"Gasto de Bs. {monto} registrado.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RegistrarFaltante(string descripcion)
        {
            _conexion.RegistrarFaltante(1, descripcion);
            TempData["Mensaje"] = "Faltante anotado.";
            return RedirectToAction("Index");
        }
    }
}