using Decolei.net.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace Decolei.net.Tests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        // Armazena um nome de banco de dados único para o factory.
        // Isso é muito importante pra garantir o isolamento quando os testes rodam em paralelo.
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptorsToRemove = services
                    .Where(d =>
                        (d.ServiceType.Name.Contains("DbContext") ||
                         d.ServiceType.Name.StartsWith("IDbContext") ||
                         d.ServiceType.Name.StartsWith("DbContextOptions")) ||
                        (d.ImplementationType != null &&
                            (d.ImplementationType.Namespace?.Contains("EntityFrameworkCore.SqlServer") == true ||
                             d.ServiceType.Namespace?.Contains("EntityFrameworkCore.SqlServer") == true))
                    )
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<DecoleiDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                });
            });
        }
    }
}