using Decolei.net.Data;
using Decolei.net.DTOs;
using Decolei.net.Models;
using Decolei.net.Tests;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace DecolaNet.Tests
{
    public abstract class BaseIntegrationTests : IDisposable
    {
        protected readonly HttpClient _client;
        protected readonly CustomWebApplicationFactory<Program> _factory;
        private readonly IServiceScope _scope;

        protected const string AuthEndpoint = "/api/Usuario";
        protected const string PacotesEndpoint = "/api/Pacotes";
        protected const string AdminEmail = "admin@decolei.net";
        protected const string AdminPassword = "SenhaAdmin123!";

        protected BaseIntegrationTests()
        {
            _factory = new CustomWebApplicationFactory<Program>();
            _client = _factory.CreateClient();
            _scope = _factory.Server.Services.CreateScope();

            var dbContext = _scope.ServiceProvider.GetRequiredService<DecoleiDbContext>();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _scope.Dispose();
            _client.Dispose();
            _factory.Dispose();
        }

        protected async Task EnsureAdminUserExistsAsync()
        {
            var userManager = _scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

            if (!await roleManager.RoleExistsAsync("ADMIN"))
            {
                await roleManager.CreateAsync(new IdentityRole<int>("ADMIN"));
            }

            if (!await roleManager.RoleExistsAsync("CLIENTE"))
            {
                await roleManager.CreateAsync(new IdentityRole<int>("CLIENTE"));
            }

            var adminUser = await userManager.FindByEmailAsync(AdminEmail);
            if (adminUser == null)
            {
                adminUser = new Usuario
                {
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    Documento = "00000000000",
                    Perfil = "ADMIN",
                    PhoneNumber = "999999999",
                    NomeCompleto = "Administrador Master",
                    EmailConfirmed = true
                };
                var createResult = await userManager.CreateAsync(adminUser, AdminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "ADMIN");
                }
                else
                {
                    throw new Exception($"Erro ao criar usuário Admin de teste: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                }
            }
        }

        protected async Task<string> LoginAndSetAuthTokenAsync(string email, string senha)
        {
            var loginDto = new LoginUsuarioDto { Email = email, Senha = senha };
            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/login", loginDto);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
            var token = responseBody.GetProperty("token").GetString();

            Assert.False(string.IsNullOrEmpty(token));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return token;
        }

        protected async Task RegisterAndConfirmUserAsync(RegistroUsuarioDto userDto)
        {
            var registrationResponse = await _client.PostAsJsonAsync($"{AuthEndpoint}/registrar", userDto);
            registrationResponse.EnsureSuccessStatusCode();

            var dbContext = _scope.ServiceProvider.GetRequiredService<DecoleiDbContext>();
            var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == userDto.Email);
            Assert.NotNull(user);
        }

        protected async Task<int> CreateAndGetPackageIdAsync(CriarPacoteViagemDto dto)
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var response = await _client.PostAsJsonAsync(PacotesEndpoint, dto);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("id").GetInt32();
        }

        protected async Task<List<JsonElement>> ExtractListFromResponse(HttpResponseMessage response)
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, json.ValueKind);
            return json.EnumerateArray().ToList();
        }
    }
}