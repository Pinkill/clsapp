using System.Diagnostics;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/ping", (IConfiguration config) =>
{
    var location = new Location();
    var connectionStrings = new[]
    {
        Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING_EUNORTH")!,
        Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING_ASIAEAST")!,
        Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING_USWEST")!,
    };

    string connectTo = string.Empty;
    long lowestLatency = long.MaxValue;

    // Test each connection string for latency
    foreach (var connectionString in connectionStrings)
    {
        if (connectionString == null) continue;

        try
        {
            var stopwatch = Stopwatch.StartNew();

            using (var sqlConnection = new SqlConnection(connectionString))
            {
                sqlConnection.Open();
                
                // Simple query to test connection latency
                var sqlCommand = new SqlCommand("SELECT 1;", sqlConnection);
                sqlCommand.ExecuteNonQuery();
            }
            
            stopwatch.Stop();
            var latency = stopwatch.ElapsedMilliseconds;
            
            if (latency < lowestLatency)
            {
                lowestLatency = latency;
                connectTo = connectionString;
            }
        }
        catch (Exception ex)
        {
            // Log or handle connection errors if needed
            Console.WriteLine($"Error connecting to database: {ex.Message}");
        }
    }

    if (connectTo == null)
    {
        return Results.Problem("No database connections are available.");
    }

    // Connect to the closest database and fetch data
    using (var sqlConnection = new SqlConnection(connectTo))
    {
        sqlConnection.Open();
        
        // Fetch only one row in table
        var sqlCommand = new SqlCommand("SELECT LocationID, Location FROM Location;", sqlConnection);
        using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
        {
            sqlDataReader.Read();
            var locationId = sqlDataReader["LocationID"].ToString();
            var locationName = sqlDataReader["Location"].ToString();

            if (string.IsNullOrEmpty(locationId)) {
                return Results.NoContent();
            }

            location = new Location
            {
                LocationID = int.Parse(locationId),
                LocationName = locationName,
            };
        }
    }

    return Results.Ok(new { ClosestDatabase = location.LocationName });
})
.WithName("Ping")
.WithOpenApi();

app.Run();


public class Location
{
    public int LocationID { get; set; }
    public string? LocationName { get; set; }
}
