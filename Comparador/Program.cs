using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container (solo una vez)
builder.Services.AddRazorPages();

// Configuraci�n de EPPlus
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

// Esta l�nea es ESENCIAL - no la elimines
app.MapRazorPages();

// Redirigir la ruta ra�z a la p�gina Index
app.MapGet("/", context => {
    context.Response.Redirect("/Index");
    return Task.CompletedTask;
});

app.Run();
