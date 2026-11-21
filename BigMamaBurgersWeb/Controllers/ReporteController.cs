using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using BigMamaBurgersWeb.Data;
using System.IO;
using System.Linq;

namespace BigMamaBurgersWeb.Controllers
{
    public class ReporteController : Controller
    {
        private readonly ConexionMySQL _conexion;

        public ReporteController(ConexionMySQL conexion)
        {
            _conexion = conexion;
        }

        public IActionResult GenerarPDF()
        {
            var resumen = _conexion.ObtenerResumenDia(1);
            var pedidos = _conexion.ObtenerPedidosAgrupados(1);
            var gastosTotales = _conexion.ObtenerTotalGastos(1);
            var faltantes = _conexion.ObtenerFaltantes(1);
            var listaGastos = _conexion.ObtenerGastosDetallados(1);

            string rutaLogo = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.jpg");
            byte[]? logoBytes = System.IO.File.Exists(rutaLogo) ? System.IO.File.ReadAllBytes(rutaLogo) : null;

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Row(r =>
                            {
                                if (logoBytes != null) r.ConstantItem(60).Image(logoBytes).FitArea();
                                r.ConstantItem(10);
                                r.RelativeItem().Column(c => {
                                    c.Item().Text("BIG MAMA BURGERS").Bold().FontSize(22).FontColor(Colors.Black);
                                    c.Item().Text("Esencia Casera").FontSize(10).FontColor(Colors.Grey.Medium);
                                });
                            });
                            row.RelativeItem().AlignRight().Column(c => {
                                c.Item().Text("CIERRE DE CAJA").Bold().FontSize(16).FontColor(Colors.Black);
                                c.Item().Text("Repartidor: Juan de la Rosa").FontSize(11).Bold().FontColor("#E53935");
                                c.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(10);
                            });
                        });
                        col.Item().PaddingTop(10).LineHorizontal(3).LineColor("#FFC107");
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                    {
                        // Resumen en 3 Columnas (Igual a la web)
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header => {
                                header.Cell().Background("#FFC107").Padding(5).Text("Total Día").Bold().FontColor(Colors.Black);
                                header.Cell().Background("#FFC107").Padding(5).Text("Efectivo").Bold().FontColor(Colors.Black);
                                header.Cell().Background("#FFC107").Padding(5).Text("QR").Bold().FontColor(Colors.Black);
                            });

                            table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(5).Text($"Bs. {resumen?.TotalDia ?? 0}").FontSize(14).Bold();
                            table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(5).Text($"Bs. {resumen?.TotalEfectivo ?? 0}").FontSize(14);
                            table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(5).Text($"Bs. {resumen?.TotalQR ?? 0}").FontSize(14);
                        });

                        // Detalle de Pedidos
                        column.Item().PaddingTop(20).Text("Detalle de Pedidos").Bold().FontSize(14).FontColor("#E53935");
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => {
                                columns.ConstantColumn(60); columns.RelativeColumn(); columns.ConstantColumn(60); columns.ConstantColumn(70);
                            });
                            table.Header(header => {
                                header.Cell().Background(Colors.Black).Padding(5).Text("Hora").FontColor("#FFC107").Bold();
                                header.Cell().Background(Colors.Black).Padding(5).Text("Productos").FontColor("#FFC107").Bold();
                                header.Cell().Background(Colors.Black).Padding(5).Text("Total").FontColor("#FFC107").Bold();
                                header.Cell().Background(Colors.Black).Padding(5).Text("Pago").FontColor("#FFC107").Bold();
                            });

                            foreach (var pedido in pedidos)
                            {
                                string detalleFormateado = pedido.Detalle.ToString().Replace(" | ", "\n");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text((string)pedido.Hora.ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(detalleFormateado);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"Bs. {pedido.TotalPedido}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text((string)pedido.Metodo.ToString());
                            }
                        });

                        // Gastos
                        if (listaGastos != null && listaGastos.Count > 0)
                        {
                            column.Item().PaddingTop(20).Text("Detalle de Gastos").Bold().FontSize(14).FontColor("#E53935");
                            column.Item().PaddingTop(5).Table(table =>
                            {
                                table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.ConstantColumn(80); });
                                foreach (var g in listaGastos)
                                {
                                    table.Cell().Padding(3).Text($"• {g.Descripcion}");
                                    table.Cell().AlignRight().Padding(3).Text($"- Bs. {g.Monto}");
                                }
                                table.Cell().Padding(3).Text("").Bold();
                                table.Cell().AlignRight().Padding(3).BorderTop(1).BorderColor(Colors.Grey.Medium).Text($"Total Gastos: Bs. {gastosTotales}").Bold();
                            });
                        }

                        // Faltantes
                        if (faltantes != null && faltantes.Count > 0)
                        {
                            column.Item().PaddingTop(20).Text("Insumos Faltantes").Bold().FontSize(14).FontColor("#E53935");
                            column.Item().PaddingTop(5).Table(table =>
                            {
                                table.ColumnsDefinition(columns => { columns.RelativeColumn(); });
                                foreach (var f in faltantes) table.Cell().Padding(3).Text($"• {f}");
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
                });
            });

            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"CierreCaja_{DateTime.Now:ddMMyyyy}.pdf");
        }
    }
}