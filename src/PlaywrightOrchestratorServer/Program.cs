var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
		builder.Services.AddControllers();
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddHttpClient();
		builder.Services.AddSingleton<PlaywrightOrchestratorServer.Services.PromptEngineeringService>();
		builder.Services.AddSingleton<PlaywrightOrchestratorServer.Services.PlaywrightMcpService>();

		// Add CORS policy to allow Angular frontend and support credentials
		builder.Services.AddCors(options =>
		{
			options.AddPolicy("AllowFrontend",
				policy => policy
					.WithOrigins("http://localhost:4200") // Update if your Angular port is different
					.AllowAnyHeader()
					.AllowAnyMethod()
					.AllowCredentials()
			);
		});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}


app.UseHttpsRedirection();
// Use CORS before controllers
app.UseCors("AllowFrontend");

// Map controllers (this exposes AutomationController and any other controllers)
app.MapControllers();

app.Run();
