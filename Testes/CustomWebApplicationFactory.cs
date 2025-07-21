using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Decolei.net.Data; // Verifique se o namespace está correto
using System.Linq;

namespace DecolaNet.Tests;

// CustomWebApplicationFactory é uma classe que estende WebApplicationFactory, e que tem como parametro Tprogram. Isso é basicamente uma classe para testar o aplicativo, 
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {


            // ===================================================================
            // INÍCIO DA CORREÇÃO FINAL (LIMPEZA COMPLETA)
            // ===================================================================

            // Encontra e remove TODOS os serviços relacionados ao DbContext.
            // Esta é a forma mais robusta de garantir que nenhum vestígio do SQL Server permaneça.
            var dbContextDescriptors = services.Where(
                d => d.ServiceType == typeof(DbContextOptions<DecoleiDbContext>) ||
                     d.ServiceType == typeof(DecoleiDbContext)).ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // ===================================================================
            // FIM DA CORREÇÃO
            // ===================================================================

            // Adicionar o DbContext novamente, mas agora 100% configurado para usar o banco em memória.
            services.AddDbContext<DecoleiDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting");
            });

            // Construir o provedor de serviços e preparar o banco de dados.
            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<DecoleiDbContext>();

                // Garante que o banco está limpo e recriado para cada teste
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
            }
        });
    }
}
