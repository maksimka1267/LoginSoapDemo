var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient(); // для IHttpClientFactory в LoginModel
// Если Swagger не нужен – можно убрать следующие две строки
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// 👉 ключевой момент:
app.MapRazorPages();

// Делаем страницу /Login домашней
app.MapGet("/", context =>
{
    context.Response.Redirect("/Login");
    return Task.CompletedTask;
});

app.Run();
