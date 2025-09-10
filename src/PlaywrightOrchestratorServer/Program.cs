var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
		builder.Services.AddControllers();
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddHttpClient();
		builder.Services.AddSingleton<PlaywrightOrchestratorServer.Services.PromptEngineeringService>(sp =>
		{
			var httpClient = sp.GetRequiredService<HttpClient>();
			var config = sp.GetRequiredService<IConfiguration>();
			return new PlaywrightOrchestratorServer.Services.PromptEngineeringService(httpClient, config);
		});
		builder.Services.AddSingleton<PlaywrightOrchestratorServer.Services.PlaywrightMcpService>();
		builder.Services.AddSingleton<PlaywrightOrchestratorServer.Services.PlaywrightMcpStdioService>();

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

// Use CORS before HTTPS redirection and controllers
app.UseCors("AllowFrontend");
app.UseHttpsRedirection();

// Map controllers (this exposes AutomationController and any other controllers)
app.MapControllers();

app.Run();
