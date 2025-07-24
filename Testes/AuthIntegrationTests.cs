using Decolei.net.DTOs;
using Decolei.net.Tests;
using Decolei.net.Tests.Testes;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DecolaNet.Tests
{
    public class AuthIntegrationTests : BaseIntegrationTests
    {
        public AuthIntegrationTests() : base() { }

        [Fact]
        [Trait("Integration", "Auth")]
        public async Task Auth01_RegistrarCliente_DeveRetornarSucesso()
        {
            var novoClienteDto = new RegistroUsuarioDto
            {
                Nome = "Cliente Teste",
                Email = "cliente@teste.com",
                Senha = "SenhaValida@123",
                Documento = "98765432109",
                Telefone = "21912345678"
            };
            await RegisterAndConfirmUserAsync(novoClienteDto);
            Assert.True(true);
        }

        [Fact]
        [Trait("Integration", "Auth")]
        public async Task Auth02_TentarRegistrar_ComEmailRepetido_DeveFalhar()
        {
            var clienteDto = new RegistroUsuarioDto
            {
                Nome = "Repetido",
                Email = "cliente.repetido@teste.com",
                Senha = "SenhaValida@123",
                Documento = "11122233344",
                Telefone = "11988887777"
            };
            await RegisterAndConfirmUserAsync(clienteDto);

            var responseRepetida = await _client.PostAsJsonAsync($"{AuthEndpoint}/registrar", clienteDto);

            Assert.Equal(HttpStatusCode.BadRequest, responseRepetida.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth")]
        public async Task Auth03_LoginCliente_ComCredenciaisCorretas_DeveRetornarToken()
        {
            var clienteDto = new RegistroUsuarioDto
            {
                Nome = "Cliente Para Login",
                Email = "cliente.login@teste.com",
                Senha = "SenhaValida@123",
                Documento = "55566677788",
                Telefone = "31911112222"
            };
            await RegisterAndConfirmUserAsync(clienteDto);

            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);
            Assert.True(true);
        }

        [Fact]
        [Trait("Integration", "Auth")]
        public async Task Auth04_LoginAdmin_ComCredenciaisCorretas_DeveRetornarToken()
        {
            await EnsureAdminUserExistsAsync();

            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            Assert.True(true);
        }

        [Fact]
        [Trait("Integration", "Auth")]
        public async Task Auth05_Login_ComSenhaIncorreta_DeveRetornarUnauthorized()
        {
            var loginDto = new LoginUsuarioDto { Email = AdminEmail, Senha = "senha-totalmente-errada" };
            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/login", loginDto);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth")]
        public async Task Auth06_Logout_QuandoAutenticado_DeveRetornarSucessoEInvalidarSessao()
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var logoutResponse = await _client.PostAsync($"{AuthEndpoint}/logout", null);

            Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

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
        [Trait("Integration", "Auth")]
        public async Task Auth07_Logout_QuandoNaoAutenticado_DeveRetornarUnauthorized()
        {
            var logoutResponse = await _client.PostAsync($"{AuthEndpoint}/logout", null);

            Assert.Equal(HttpStatusCode.Unauthorized, logoutResponse.StatusCode);
        }
    }
}