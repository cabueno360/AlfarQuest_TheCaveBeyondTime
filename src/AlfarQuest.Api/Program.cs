using AlfarQuest.Api.Auth;
using AlfarQuest.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("MySql")
           ?? "server=localhost;port=3306;database=alfar_quest;user=root;password=change_me;";

// Oracle's MySql.EntityFrameworkCore provider (EF Core 10). Unlike Pomelo it
// detects server capabilities itself, so no explicit ServerVersion is needed.
builder.Services.AddDbContext<GameDbContext>(opt => opt.UseMySQL(conn));

// Accounts, sessions, profiles, avatars and the rate limiter that guards them.
builder.Services.AddPlayerAccounts(builder.Configuration);

builder.Services.AddScoped<AlfarQuest.Api.Services.SaveService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The Blazor client runs on a different origin, so it needs CORS to call this at
// all. Named origins in production: with bearer tokens an over-wide policy lets
// any page a player visits spend their session. Wide open only in Development,
// where the client's port changes between runs.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (builder.Environment.IsDevelopment() || allowedOrigins.Length == 0)
        p.SetIsOriginAllowed(_ => true);
    else
        p.WithOrigins(allowedOrigins);

    p.AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

// Create the schema and seed heroes on first run. Wrapped so the API still
// starts (returning 500s the client tolerates) if MySQL isn't up yet.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        // A development escape hatch: the schema is created with EnsureCreated,
        // which never alters an existing database, so a change to the model needs
        // the dev database rebuilt. Guarded by config and off by default — it
        // deletes everything, so it must never fire in production. Set
        // Dev__ResetDatabase=true for one run after a schema change.
        if (builder.Configuration.GetValue<bool>("Dev:ResetDatabase"))
        {
            app.Logger.LogWarning("Dev:ResetDatabase is set — dropping and recreating the schema.");
            db.Database.EnsureDeleted();
        }
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not initialise the MySQL database. Is the server running?");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

// Order matters: the limiter runs before authentication so a flood of bad tokens
// is turned away without costing a database lookup each.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
