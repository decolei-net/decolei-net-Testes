using DecolaNet.Tests;
using Decolei.net.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Decolei.net.Tests.Testes
{
    public class PacoteIntegrationTests : BaseIntegrationTests 
    {
        public PacoteIntegrationTests() : base() { }

        [Fact]
        [Trait("Integration", "Pacotes")]
        public async Task Pkg01_CriarPacote_ComoAdmin_DeveRetornarCreated()
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var novoPacoteDto = new CriarPacoteViagemDto
            {
                Titulo = "Viagem para a Lua",
                Destino = "Lua",
                Valor = 99999.99m,
                DataInicio = DateTime.UtcNow.AddMonths(6),
                DataFim = DateTime.UtcNow.AddMonths(6).AddDays(7)
            };
            var response = await _client.PostAsJsonAsync(PacotesEndpoint, novoPacoteDto);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes")]
        public async Task Pkg02_TentarCriarPacote_ComoCliente_DeveRetornarForbidden()
        {
            var clienteDto = new RegistroUsuarioDto { Email = "cliente.comum@teste.com", Nome = "Comum", Documento = "11111111111", Senha = "senha1", Telefone = "1" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var pacoteDto = new CriarPacoteViagemDto { Titulo = "Teste Cliente", Destino = "Falha", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) };
            var response = await _client.PostAsJsonAsync(PacotesEndpoint, pacoteDto);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes")]
        public async Task Pkg03_TentarCriarPacote_SemAutenticacao_DeveRetornarUnauthorized()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var pacoteDto = new CriarPacoteViagemDto { Titulo = "Teste Sem Auth", Destino = "Falha", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) };
            var response = await _client.PostAsJsonAsync(PacotesEndpoint, pacoteDto);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes")]
        public async Task Pkg04_ListarPacotes_Publico_DeveRetornarOkComListaNaoVazia()
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            await _client.PostAsJsonAsync(PacotesEndpoint, new CriarPacoteViagemDto
            {
                Titulo = "Pacote de Teste para Lista",
                Destino = "Terra",
                Valor = 100,
                DataInicio = DateTime.Now,
                DataFim = DateTime.Now.AddDays(1)
            });

            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync(PacotesEndpoint);
            response.EnsureSuccessStatusCode();

            var pacotesArray = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(JsonValueKind.Array, pacotesArray.ValueKind);
            Assert.True(pacotesArray.EnumerateArray().Any(), "A lista de pacotes não deveria estar vazia após a criação de um pacote.");
        }

        [Fact]
        [Trait("Integration", "Pacotes")]
        public async Task Pkg05_TentarCriarPacote_ComDadosInvalidos_DeveRetornarBadRequest()
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var pacoteInvalido = new { Destino = "Marte", Valor = 150000m };
            var response = await _client.PostAsJsonAsync(PacotesEndpoint, pacoteInvalido);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}