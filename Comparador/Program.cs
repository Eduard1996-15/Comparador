using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container (solo una vez)
builder.Services.AddRazorPages();

// Configuración de EPPlus
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Esta línea es ESENCIAL - no la elimines
app.MapRazorPages();

// Redirigir la ruta raíz a la página Index
app.MapGet("/", context => {
    context.Response.Redirect("/Index");
    return Task.CompletedTask;
});

app.Run();
