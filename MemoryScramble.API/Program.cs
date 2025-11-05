var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCors();


var app = builder.Build();


app.MapGet("/", () => "Hello World!");

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseStaticFiles();


app.Run();
