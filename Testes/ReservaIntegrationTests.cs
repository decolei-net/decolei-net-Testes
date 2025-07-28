using Decolei.net.DTOs;
using Decolei.net.Tests;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Decolei.net.Tests.Testes
{
    public class ReservaIntegrationTests : BaseIntegrationTests
    {
        public ReservaIntegrationTests() : base() { }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv01_CriarReserva_ComoCliente_DeveRetornarCreated()
        {
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P1", Destino = "D1", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var clienteDto = new RegistroUsuarioDto { Nome = "Cliente Feliz", Email = "feliz@teste.com", Senha = "SenhaValida123", Documento = "11111111111", Telefone = "11111111111" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var response = await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv02_CriarReserva_ParaPacoteInexistente_DeveRetornarNotFound()
        {
            var clienteDto = new RegistroUsuarioDto { Nome = "Cliente Azarado", Email = "azarado@teste.com", Senha = "SenhaValida123", Documento = "22222222222", Telefone = "22222222222" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var response = await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = 9999 });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv03_GetAllReservas_ComoAdmin_DeveRetornarLista()
        {
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P2", Destino = "D2", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            await RegisterAndConfirmUserAsync(new RegistroUsuarioDto { Nome = "C1", Email = "c1@teste.com", Senha = "senha123", Documento = "33333333333", Telefone = "33333333333" });
            await LoginAndSetAuthTokenAsync("c1@teste.com", "senha123");
            await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });

            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var response = await _client.GetAsync("/api/Reserva");

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.NotEmpty(body);
        }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv04_GetAllReservas_ComoCliente_DeveRetornarForbidden()
        {
            var clienteDto = new RegistroUsuarioDto { Nome = "Cliente Curioso", Email = "curioso@teste.com", Senha = "senha123", Documento = "44444444444", Telefone = "44444444444" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var response = await _client.GetAsync("/api/Reserva");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv05_GetMinhasReservas_DeveRetornarApenasAsDoUsuarioLogado()
        {
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P3", Destino = "D3", Valor = 1, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var userADto = new RegistroUsuarioDto { Nome = "User A", Email = "a@teste.com", Senha = "senha123", Documento = "55555555555", Telefone = "55555555555" };
            await RegisterAndConfirmUserAsync(userADto);
            var idUserA = await GetUserIdByEmail(userADto.Email);
            await LoginAndSetAuthTokenAsync(userADto.Email, userADto.Senha);
            await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });

            var userBDto = new RegistroUsuarioDto { Nome = "User B", Email = "b@teste.com", Senha = "senha123", Documento = "66666666666", Telefone = "66666666666" };
            await RegisterAndConfirmUserAsync(userBDto);
            await LoginAndSetAuthTokenAsync(userBDto.Email, userBDto.Senha);
            await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });

            await LoginAndSetAuthTokenAsync(userADto.Email, userADto.Senha);
            var response = await _client.GetAsync("/api/Reserva/minhas-reservas");

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.Single(body);
            Assert.Equal(idUserA, body.First().GetProperty("usuario").GetProperty("id").GetInt32());
        }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv06_GetReservaPorId_ComoDono_DeveRetornarOk()
        {
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P4", Destino = "D4", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var clienteDto = new RegistroUsuarioDto { Nome = "Dono", Email = "dono@teste.com", Senha = "senha123", Documento = "77777777777", Telefone = "77777777777" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);
            var reservaResponse = await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });
            var reservaId = (await reservaResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            var response = await _client.GetAsync($"/api/Reserva/{reservaId}");

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(reservaId, body.GetProperty("id").GetInt32());
        }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv07_GetReservaPorId_ComoNaoDono_DeveRetornarForbidden()
        {
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P5", Destino = "D5", Valor = 1, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var userADto = new RegistroUsuarioDto { Nome = "Dono", Email = "dono.reserva@teste.com", Senha = "senha123", Documento = "88888888888", Telefone = "88888888888" };
            await RegisterAndConfirmUserAsync(userADto);
            await LoginAndSetAuthTokenAsync(userADto.Email, userADto.Senha);
            var reservaResponse = await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });
            var reservaId = (await reservaResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            var userBDto = new RegistroUsuarioDto { Nome = "Invasor", Email = "invasor@teste.com", Senha = "senha123", Documento = "99999999999", Telefone = "99999999999" };
            await RegisterAndConfirmUserAsync(userBDto);
            await LoginAndSetAuthTokenAsync(userBDto.Email, userBDto.Senha);

            var response = await _client.GetAsync($"/api/Reserva/{reservaId}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv08_AtualizarStatus_ComoAdmin_DeveRetornarNoContent()
        {
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P6", Destino = "D6", Valor = 1, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var clienteDto = new RegistroUsuarioDto { Nome = "Cliente Teste", Email = "clienteteste@teste.com", Senha = "senha123", Documento = "10101010101", Telefone = "10101010101" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);
            var reservaResponse = await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });
            var reservaId = (await reservaResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var response = await _client.PutAsJsonAsync($"/api/Reserva/{reservaId}", new UpdateReservaDto { Status = "CONFIRMADA" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Reservas")]
        public async Task Rsv09_AtualizarStatus_ComoCliente_DeveRetornarForbidden()
        {
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P7", Destino = "D7", Valor = 1, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var clienteDto = new RegistroUsuarioDto { Nome = "Cliente Esperto", Email = "esperto@teste.com", Senha = "senha123", Documento = "12121212121", Telefone = "12121212121" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);
            var reservaResponse = await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });
            var reservaId = (await reservaResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            var response = await _client.PutAsJsonAsync($"/api/Reserva/{reservaId}", new UpdateReservaDto { Status = "CONFIRMADA" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Reservas - Lógica de Valor")]
        public async Task Rsv10_CriarReserva_SemViajantes_ValorTotalDeveSerIgualAoPacote()
        {
            decimal valorDoPacote = 1250.50m;
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "Sozinho", Destino = "Ilha", Valor = valorDoPacote, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var clienteDto = new RegistroUsuarioDto { Nome = "Viajante Solitário", Email = "solitario@teste.com", Senha = "SenhaValida123", Documento = "13131313131", Telefone = "13131313131" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var reservaDto = new CriarReservaDto
            {
                PacoteViagemId = pacoteId,
                Viajantes = new List<ViajanteDto>()
            };

            var response = await _client.PostAsJsonAsync("/api/Reserva", reservaDto);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(valorDoPacote, body.GetProperty("valorTotal").GetDecimal());
        }

        [Fact]
        [Trait("Integration", "Reservas - Lógica de Valor")]
        public async Task Rsv11_CriarReserva_ComViajantes_ValorTotalDeveSerMultiplicado()
        {
            decimal valorDoPacote = 1000m;
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "Em Grupo", Destino = "Excursão", Valor = valorDoPacote, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var clienteDto = new RegistroUsuarioDto { Nome = "Organizador", Email = "organizador@teste.com", Senha = "SenhaValida123", Documento = "14141414141", Telefone = "14141414141" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var viajantes = new List<ViajanteDto>
            {
                new ViajanteDto { Nome = "Amigo 1", Documento = "A1" },
                new ViajanteDto { Nome = "Amigo 2", Documento = "A2" }
            };

            decimal valorEsperado = valorDoPacote * (1 + viajantes.Count); // 1000 * 3 = 3000

            var reservaDto = new CriarReservaDto
            {
                PacoteViagemId = pacoteId,
                Viajantes = viajantes
            };

            var response = await _client.PostAsJsonAsync("/api/Reserva", reservaDto);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(valorEsperado, body.GetProperty("valorTotal").GetDecimal());
        }

        [Fact]
        [Trait("Integration", "Reservas - Segurança")]
        public async Task Rsv12_CriarReserva_SemAutenticacao_DeveRetornarUnauthorized()
        {
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P10", Destino = "D10", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            _client.DefaultRequestHeaders.Authorization = null; // Garante que não está logado

            var response = await _client.PostAsJsonAsync("/api/Reserva", new CriarReservaDto { PacoteViagemId = pacoteId });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

    }
}