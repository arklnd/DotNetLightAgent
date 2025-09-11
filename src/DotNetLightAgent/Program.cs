using DotNetLightAgent.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for web frontends
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register the agent service
builder.Services.AddSingleton<IAgentService, AgentService>();

// Configure logging
builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Information));

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger in all environments for this demo
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();