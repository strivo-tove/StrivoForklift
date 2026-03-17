// using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
// using StrivoForklift.Data;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Database registration is commented out while we diagnose queue ingestion.
        // Re-enable once the queue trigger is confirmed working and a SQL connection is available.
        //
        // var connectionString = context.Configuration.GetConnectionString("SqlConnection")
        //     ?? throw new InvalidOperationException(
        //         "A 'SqlConnection' connection string must be provided in configuration.");
        //
        // services.AddDbContext<ForkliftDbContext>(options =>
        //     options.UseSqlServer(connectionString));
    })
    .Build();

// Commented out while database operations are disabled.
// using (var scope = host.Services.CreateScope())
// {
//     var dbContext = scope.ServiceProvider.GetRequiredService<ForkliftDbContext>();
//     dbContext.Database.EnsureCreated();
// }

host.Run();
