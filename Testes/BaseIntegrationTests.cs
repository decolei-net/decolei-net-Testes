using Azure;
using Decolei.net.Data;
using Decolei.net.DTOs;
using Decolei.net.Models;
using Decolei.net.Services;
using Decolei.net.Tests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Decolei.net.Tests.Testes
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

        protected async Task<int?> GetUserIdByEmail(string email)
        {
            using var scope = _factory.Server.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DecoleiDbContext>();
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            return user?.Id;
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

        protected async Task<int> CreatePackageAndGetIdAsync(CriarPacoteViagemDto dto)
        {
            // Este método precisa estar logado como admin para criar o pacote
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var response = await _client.PostAsJsonAsync(PacotesEndpoint, dto);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("id").GetInt32();
        }

        protected async Task<string?> GeneratePasswordResetTokenForUserAsync(string email)
        {
            using var scope = _factory.Server.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return null;
            }
            return await userManager.GeneratePasswordResetTokenAsync(user);
        }

        protected async Task<(int reservaId, decimal valorPacote, string userEmail, string userPassword)> CreatePackageAndReservationAsync()
        {
            var valorPacote = 150.75m;
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P Teste", Destino = "D Teste", Valor = valorPacote, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });

            var userPassword = "PasswordValida123";
            var userDto = new RegistroUsuarioDto
            {
                Nome = $"Cliente Reserva {Guid.NewGuid()}",
                Email = $"cliente.reserva.{Guid.NewGuid()}@teste.com",
                Senha = userPassword,
                Documento = new Random().NextInt64(10000000000, 99999999999).ToString(),
                Telefone = "11999999999"
            };
            await RegisterAndConfirmUserAsync(userDto);

            await LoginAndSetAuthTokenAsync(userDto.Email, userDto.Senha);

            var reservaDto = new CriarReservaDto { PacoteViagemId = pacoteId };
            var response = await _client.PostAsJsonAsync("/api/Reserva", reservaDto);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var reservaId = body.GetProperty("id").GetInt32();

            return (reservaId, valorPacote, userDto.Email, userDto.Senha);
        }

        protected async Task<(int usuarioId, int pacoteId, int? reservaId, string userEmail, string userPassword)>
        CreateValidScenarioForReviewAsync(TimeSpan? travelEndDateOffset = null, string statusReserva = "CONFIRMADA", bool criarReserva = true)
        {
            // Define a data de fim da viagem. Padrão: ontem.
            var travelEndDate = DateTime.UtcNow + (travelEndDateOffset ?? TimeSpan.FromDays(-1));

            var userPassword = "PasswordValida123";
            var userDto = new RegistroUsuarioDto
            {
                Nome = $"Cliente Avaliador {Guid.NewGuid()}",
                Email = $"avaliador.{Guid.NewGuid()}@teste.com",
                Senha = userPassword,
                Documento = new Random().NextInt64(10000000000, 99999999999).ToString(),
                Telefone = "11988887777"
            };
            await RegisterAndConfirmUserAsync(userDto);
            var usuarioId = (await GetUserIdByEmail(userDto.Email)).Value;

            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P Teste", Destino = "D Teste", Valor = 100, DataInicio = DateTime.UtcNow.AddDays(-10), DataFim = travelEndDate });

            int? reservaId = null;
            if (criarReserva)
            {
                await LoginAndSetAuthTokenAsync(userDto.Email, userDto.Senha);

                var reservaDto = new CriarReservaDto { PacoteViagemId = pacoteId };
                var reservaResponse = await _client.PostAsJsonAsync("/api/Reserva", reservaDto);
                reservaResponse.EnsureSuccessStatusCode();
                var reservaBody = await reservaResponse.Content.ReadFromJsonAsync<JsonElement>();
                reservaId = reservaBody.GetProperty("id").GetInt32();

                // Simula a alteração do status da reserva
                using (var scope = _factory.Server.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<DecoleiDbContext>();
                    var reserva = await dbContext.Reservas.FindAsync(reservaId.Value);
                    reserva.Status = statusReserva; // Usa o status passado como parâmetro
                    await dbContext.SaveChangesAsync();
                }
            }

            // Retorna nullable reservaId
            return (usuarioId, pacoteId, reservaId, userDto.Email, userDto.Senha);
        }
    }
}