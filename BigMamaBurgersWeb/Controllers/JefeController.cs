using Microsoft.AspNetCore.Mvc;
using BigMamaBurgersWeb.Data;

namespace BigMamaBurgersWeb.Controllers
{
    public class JefeController : Controller
    {
        private readonly ConexionMySQL _conexion;

        public JefeController(ConexionMySQL conexion)
        {
            _conexion = conexion;
        }

        public IActionResult Index()
        {
            // Traemos los datos de las Vistas SQL
            ViewBag.Reporte = _conexion.ObtenerReporteDiario();
            ViewBag.StockBajo = _conexion.ObtenerStockBajo();

            return View();
        }
    }
}