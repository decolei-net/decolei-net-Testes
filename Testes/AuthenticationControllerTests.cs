using Decolei.net.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DecolaNet.Tests;

// Indica que esta classe de testes usará a nossa fábrica customizada
public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Integration", "Auth")]
    public async Task Auth01_E_Auth02_RegistrarCliente_DeveRetornarSucesso_E_FalharAoRepetirEmail()
    {
        // --- Cenário Auth-01: Registro de novo cliente com sucesso ---
        var novoClienteDto = new RegistroUsuarioDto
        {
            Nome = "Cliente Teste",
            Email = "cliente@teste.com",
            Senha = "Senha@123",
            Documento = "12345678901",
            Telefone = "string"
        };

        var response = await _client.PostAsJsonAsync("/api/Usuario/registrar", novoClienteDto);

        // Verificação (Assert)
        response.EnsureSuccessStatusCode(); // Garante que o status é 2xx
        var responseString = await response.Content.ReadAsStringAsync();
        Assert.Contains("Usuário cliente registrado com sucesso!", responseString);


        // --- Cenário Auth-02: Tentar registrar com e-mail já existente ---
        var responseRepetida = await _client.PostAsJsonAsync("/api/Usuario/registrar", novoClienteDto);

        // Verificação (Assert)
        Assert.Equal(HttpStatusCode.BadRequest, responseRepetida.StatusCode);
        var errorString = await responseRepetida.Content.ReadAsStringAsync();
        Assert.Contains("Este e-mail já está em uso.", errorString);
    }

    [Fact]
    [Trait("Integration", "Auth")]
    public async Task Auth03_LoginCliente_ComCredenciaisCorretas_DeveRetornarToken()
    {
        // Arrange: Primeiro, precisamos garantir que o cliente exista.
        var clienteDto = new RegistroUsuarioDto
        {
            Nome = "Cliente Para Login",
            Email = "cliente.login@teste.com",
            Senha = "Senha@123",
            Documento = "09876543210"
        };
        await _client.PostAsJsonAsync("/api/Usuario/registrar", clienteDto);

        var loginDto = new LoginUsuarioDto
        {
            Email = "cliente.login@teste.com",
            Senha = "Senha@123"
        };

        // Act: Tentar fazer o login
        var response = await _client.PostAsJsonAsync("/api/Usuario/login", loginDto);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = responseBody.GetProperty("token").GetString();

        Assert.False(string.IsNullOrEmpty(token)); // Verifica se o token não é nulo ou vazio
    }

    [Fact]
    [Trait("Integration", "Auth")]
    public async Task Auth04_LoginAdmin_ComCredenciaisCorretas_DeveRetornarToken()
    {
        // Arrange: O usuário admin já é criado pela aplicação (seed)
        var loginDto = new LoginUsuarioDto
        {
            Email = "admin@decolei.net",
            Senha = "SenhaAdmin123!" // Senha definida no seu Program.cs
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Usuario/login", loginDto);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = responseBody.GetProperty("token").GetString();

        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    [Trait("Integration", "Auth")]
    public async Task Auth05_Login_ComSenhaIncorreta_DeveRetornarUnauthorized()
    {
        // Arrange
        var loginDto = new LoginUsuarioDto
        {
            Email = "admin@decolei.net",
            Senha = "senhaerrada123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Usuario/login", loginDto);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var errorString = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email ou senha inválidos.", errorString);
    }
}