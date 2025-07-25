using Decolei.net.DTOs;
using Decolei.net.Tests;
using Decolei.net.Tests.Testes;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Decolei.net.Tests.Testes
{
    public class AuthIntegrationTests : BaseIntegrationTests
    {
        public AuthIntegrationTests() : base() { }

        [Fact]
        [Trait("Integration", "Auth - Registro")]
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
        [Trait("Integration", "Auth - Registro")]
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
        [Trait("Integration", "Auth - Login")]
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
        [Trait("Integration", "Auth - Login")]
        public async Task Auth04_LoginAdmin_ComCredenciaisCorretas_DeveRetornarToken()
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            Assert.True(true);
        }

        [Fact]
        [Trait("Integration", "Auth - Login")]
        public async Task Auth05_Login_ComSenhaIncorreta_DeveRetornarUnauthorized()
        {
            await EnsureAdminUserExistsAsync(); // Garante que o admin exista primeiro
            var loginDto = new LoginUsuarioDto { Email = AdminEmail, Senha = "senha-totalmente-errada" };
            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/login", loginDto);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth - Registro")]
        public async Task Auth06_TentarRegistrar_ComDocumentoRepetido_DeveFalhar()
        {
            var documento = "12345678901";
            var cliente1Dto = new RegistroUsuarioDto
            {
                Nome = "Pessoa A",
                Email = "pessoa.a@teste.com",
                Senha = "SenhaValida@123",
                Documento = documento,
                Telefone = "111"
            };
            await RegisterAndConfirmUserAsync(cliente1Dto);

            var cliente2Dto = new RegistroUsuarioDto
            {
                Nome = "Pessoa B",
                Email = "pessoa.b@teste.com",
                Senha = "SenhaValida@123",
                Documento = documento,
                Telefone = "222"
            };
            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/registrar", cliente2Dto);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errorBody = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("Este documento já está em uso.", errorBody.GetProperty("erro").GetString());
        }

        [Fact]
        [Trait("Integration", "Auth - Segurança Admin")]
        public async Task Auth07_TentarRegistrarAdmin_ComoCliente_DeveRetornarForbidden()
        {
            var clienteDto = new RegistroUsuarioDto { Nome = "Cliente", Email = "cliente.comum@teste.com", Senha = "senha123", Documento = "333", Telefone = "333" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var novoAdminDto = new RegistroUsuarioDto { Nome = "Hacker", Email = "hacker@teste.com", Senha = "hack", Documento = "444", Telefone = "444" };
            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/registrar-admin", novoAdminDto);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth - Segurança Admin")]
        public async Task Auth08_TentarRegistrarAdmin_SemAutenticacao_DeveRetornarUnauthorized()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var novoAdminDto = new RegistroUsuarioDto { Nome = "Anonimo", Email = "anonimo@teste.com", Senha = "anon", Documento = "555", Telefone = "555" };

            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/registrar-admin", novoAdminDto);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth - Admin")]
        public async Task Auth09_AdminRegistraAdmin_DeveRetornarSucesso()
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var novoAdminDto = new RegistroUsuarioDto
            {
                Nome = "Admin Jr.",
                Email = "admin.jr@teste.com",
                Senha = "SenhaAdminJr123",
                Documento = "99988877700",
                Telefone = "51912345678"
            };

            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/registrar-admin", novoAdminDto);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Usuário administrador registrado com sucesso!", responseBody.GetProperty("mensagem").GetString());
        }

        // NOVO TESTE
        [Fact]
        [Trait("Integration", "Auth - Admin")]
        public async Task Auth10_ListarUsuarios_ComoAdmin_DeveRetornarOk()
        {
            await EnsureAdminUserExistsAsync();
            await RegisterAndConfirmUserAsync(new RegistroUsuarioDto { Nome = "Cliente Teste", Email = "c@t.com", Senha = "senha123", Documento = "666", Telefone = "666" });
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var response = await _client.GetAsync(AuthEndpoint);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.True(body.Count >= 2);
        }

        [Fact]
        [Trait("Integration", "Auth - Segurança Admin")]
        public async Task Auth11_TentarListarUsuarios_ComoCliente_DeveRetornarForbidden()
        {
            var clienteDto = new RegistroUsuarioDto { Nome = "Cliente X", Email = "x@t.com", Senha = "senha123", Documento = "777", Telefone = "777" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var response = await _client.GetAsync(AuthEndpoint);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth - Admin")]
        public async Task Auth12_ObterUsuarioPorId_ComoAdmin_DeveRetornarOk()
        {
            var clienteDto = new RegistroUsuarioDto { Nome = "Alvo", Email = "alvo@t.com", Senha = "senha123", Documento = "888", Telefone = "888" };
            await RegisterAndConfirmUserAsync(clienteDto);
            var userId = await GetUserIdByEmail(clienteDto.Email);
            Assert.NotNull(userId);

            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var response = await _client.GetAsync($"{AuthEndpoint}/{userId}");

            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(clienteDto.Email, responseBody.GetProperty("email").GetString());
        }

        [Fact]
        [Trait("Integration", "Auth - Logout")]
        public async Task Auth13_Logout_QuandoAutenticado_DeveRetornarSucesso()
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var logoutResponse = await _client.PostAsync($"{AuthEndpoint}/logout", null);

            logoutResponse.EnsureSuccessStatusCode();
            var body = await logoutResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Logout realizado com sucesso!", body.GetProperty("mensagem").GetString());
        }

        [Fact]
        [Trait("Integration", "Auth - Logout")]
        public async Task Auth14_TentarAcessarEndpointProtegido_SemToken_DeveFalhar()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync(AuthEndpoint);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth - Logout")]
        public async Task Auth15_TentarLogout_QuandoNaoAutenticado_DeveRetornarUnauthorized()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var logoutResponse = await _client.PostAsync($"{AuthEndpoint}/logout", null);
            Assert.Equal(HttpStatusCode.Unauthorized, logoutResponse.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth - Recuperação de Senha")]
        public async Task Auth16_RecuperarSenha_ParaUsuarioInexistente_DeveRetornarOk()
        {
            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/recuperar-senha", new RecuperarSenhaDto { Email = "fantasma@teste.com" });

            response.EnsureSuccessStatusCode();
        }

        [Fact]
        [Trait("Integration", "Auth - Recuperação de Senha")]
        public async Task Auth17_RedefinirSenha_ComTokenValido_DevePermitirLoginComNovaSenha()
        {
            var oldPassword = "SenhaAntiga123";
            var newPassword = "SenhaNova456!";
            var clienteDto = new RegistroUsuarioDto { Nome = "Muda Senha", Email = "mudasenha@teste.com", Senha = oldPassword, Documento = "2222", Telefone = "2222" };
            await RegisterAndConfirmUserAsync(clienteDto);

            var token = await GeneratePasswordResetTokenForUserAsync(clienteDto.Email);
            Assert.NotNull(token);

            var redefinirDto = new RedefinirSenhaDto { Email = clienteDto.Email, Token = token!, NovaSenha = newPassword };
            var responseRedefinir = await _client.PostAsJsonAsync($"{AuthEndpoint}/redefinir-senha", redefinirDto);
            responseRedefinir.EnsureSuccessStatusCode();

            var loginDtoNovo = new LoginUsuarioDto { Email = clienteDto.Email, Senha = newPassword };
            var responseLoginNovo = await _client.PostAsJsonAsync($"{AuthEndpoint}/login", loginDtoNovo);
            responseLoginNovo.EnsureSuccessStatusCode();

            var loginDtoAntigo = new LoginUsuarioDto { Email = clienteDto.Email, Senha = oldPassword };
            var responseLoginAntigo = await _client.PostAsJsonAsync($"{AuthEndpoint}/login", loginDtoAntigo);
            Assert.Equal(HttpStatusCode.Unauthorized, responseLoginAntigo.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Auth - Recuperação de Senha")]
        public async Task Auth18_RedefinirSenha_ComTokenInvalido_DeveFalhar()
        {
            var clienteDto = new RegistroUsuarioDto { Nome = "Token Ruim", Email = "tokenruim@teste.com", Senha = "senha123", Documento = "3333", Telefone = "3333" };
            await RegisterAndConfirmUserAsync(clienteDto);

            var redefinirDto = new RedefinirSenhaDto { Email = clienteDto.Email, Token = "TOKEN_FALSO_E_INVALIDO", NovaSenha = "novaSenha123" };
            var response = await _client.PostAsJsonAsync($"{AuthEndpoint}/redefinir-senha", redefinirDto);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}