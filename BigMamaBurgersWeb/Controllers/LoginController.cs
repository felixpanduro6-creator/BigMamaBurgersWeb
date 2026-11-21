using Microsoft.AspNetCore.Mvc;
using BigMamaBurgersWeb.Data;
using BigMamaBurgersWeb.Models;

namespace BigMamaBurgersWeb.Controllers
{
    public class LoginController : Controller
    {
        private readonly ConexionMySQL _conexion;

        public LoginController(ConexionMySQL conexion)
        {
            _conexion = conexion;
        }

        // GET: Muestra la pantalla
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // POST: Recibe los datos
        [HttpPost]
        public IActionResult Index(string username, string password)
        {
            var usuario = _conexion.ValidarLogin(username, password);

            if (usuario != null)
            {
                if (usuario.IdRol == 1) // Jefe
                {
                    return RedirectToAction("Index", "Jefe");
                }
                else if (usuario.IdRol == 2) // Carrito
                {
                    // AQUÍ CAMBIAMOS "Repartidor" POR "JuandelaRosa"
                    return RedirectToAction("Index", "JuandelaRosa");
                }
            }

            ViewBag.Error = "Usuario o contraseña incorrectos.";
            return View();
        }
    }
}