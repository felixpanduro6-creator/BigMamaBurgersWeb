using BigMamaBurgersWeb.Data; // <-- Agregamos la referencia a tu carpeta Data

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container.
builder.Services.AddControllersWithViews();

// 2. REGISTRO DE LA CONEXIÓN A MYSQL (Muy importante)
builder.Services.AddScoped<ConexionMySQL>();

var app = builder.Build();

// 3. Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// 4. Configuración de la ruta por defecto
// Cambiamos el controlador por defecto para que cargue el Login primero
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();