using Decolei.net.DTOs;
using Decolei.net.Tests;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Decolei.net.Tests.Testes
{
    public class PagamentoIntegrationTests : BaseIntegrationTests
    {
        public PagamentoIntegrationTests() : base() { }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_01_CriarPagamento_ComDadosValidos_DeveRetornarOk()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var pagamentoDto = new
            {
                ReservaId = reservaId,
                NomeCompleto = "Cliente Teste Válido",
                Cpf = "12345678901",
                Metodo = "CREDITO",
                Valor = valorPacote,
                Parcelas = 1,
                NumeroCartao = "1111222233334444",
                Email = userEmail
            };

            var response = await _client.PostAsJsonAsync("/pagamentos", pagamentoDto);

            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_02_CriarPagamento_ParaReservaJaPaga_DeveRetornarBadRequest()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagamentoDto = new
            {
                ReservaId = reservaId,
                NomeCompleto = "C Teste",
                Cpf = "12345678901",
                Metodo = "PIX",
                Valor = valorPacote,
                Email = userEmail
            };

            await _client.PostAsJsonAsync("/pagamentos", pagamentoDto);
            var response = await _client.PostAsJsonAsync("/pagamentos", pagamentoDto);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_03_CriarPagamento_ComoNaoDonoDaReserva_DeveRetornarBadRequest()
        {
            var (reservaId, valorPacote, _, _) = await CreatePackageAndReservationAsync();
            var outroClienteDto = new RegistroUsuarioDto { Nome = "Invasor", Email = "invasor@teste.com", Senha = "senha123", Documento = "98765432109", Telefone = "111" };
            await RegisterAndConfirmUserAsync(outroClienteDto);
            await LoginAndSetAuthTokenAsync(outroClienteDto.Email, outroClienteDto.Senha);

            var pagamentoDto = new
            {
                ReservaId = reservaId,
                NomeCompleto = "Tentativa Indevida",
                Cpf = "09876543210",
                Metodo = "DEBITO",
                Valor = valorPacote,
                Email = outroClienteDto.Email
            };

            var response = await _client.PostAsJsonAsync("/pagamentos", pagamentoDto);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_04_ObterStatus_ComoAdmin_DeveRetornarOk()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagamentoDto = new
            {
                ReservaId = reservaId,
                NomeCompleto = "C Teste",
                Cpf = "12345678901",
                Metodo = "PIX",
                Valor = valorPacote,
                Email = userEmail
            };
            var pagResponse = await _client.PostAsJsonAsync("/pagamentos", pagamentoDto);
            pagResponse.EnsureSuccessStatusCode();
            var pagamentoId = (await pagResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var response = await _client.GetAsync($"/pagamentos/status/{pagamentoId}");
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_05_ObterStatus_ComoCliente_DeveRetornarForbidden()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagamentoDto = new
            {
                ReservaId = reservaId,
                NomeCompleto = "C Teste",
                Cpf = "12345678901",
                Metodo = "PIX",
                Valor = valorPacote,
                Email = userEmail
            };
            var pagResponse = await _client.PostAsJsonAsync("/pagamentos", pagamentoDto);
            pagResponse.EnsureSuccessStatusCode();
            var pagamentoId = (await pagResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            var response = await _client.GetAsync($"/pagamentos/status/{pagamentoId}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_06_AtualizarStatus_ComoAdmin_DeveRetornarOk()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagamentoDto = new
            {
                ReservaId = reservaId,
                NomeCompleto = "C Teste",
                Cpf = "12345678901",
                Metodo = "PIX", 
                Valor = valorPacote,
                Email = userEmail
            };
            var pagResponse = await _client.PostAsJsonAsync("/pagamentos", pagamentoDto);
            pagResponse.EnsureSuccessStatusCode();
            var pagamentoId = (await pagResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var statusUpdateDto = new { Status = "APROVADO" };

            var response = await _client.PutAsJsonAsync($"/pagamentos/{pagamentoId}", statusUpdateDto);
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_07_AtualizarStatus_ComoCliente_DeveRetornarForbidden()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagamentoDto = new
            {
                ReservaId = reservaId,
                NomeCompleto = "C Teste",
                Cpf = "12345678901",
                Metodo = "PIX",
                Valor = valorPacote,
                Email = userEmail
            };
            var pagResponse = await _client.PostAsJsonAsync("/pagamentos", pagamentoDto);
            pagResponse.EnsureSuccessStatusCode();
            var pagamentoId = (await pagResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            var statusUpdateDto = new { Status = "APROVADO" };

            var response = await _client.PutAsJsonAsync($"/pagamentos/{pagamentoId}", statusUpdateDto);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}