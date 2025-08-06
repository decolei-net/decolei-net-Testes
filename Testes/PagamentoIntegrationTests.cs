using Decolei.net.DTOs;
using Decolei.net.Enums;
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
        public async Task Pag_01_CriarPagamento_ComDadosValidos_DeveRetornarOkEStatusAprovado()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var pagamentoDto = new PagamentoEntradaDTO
            {
                ReservaId = reservaId,
                NomeCompleto = "Cliente Teste Válido",
                Cpf = "12345678901",
                Metodo = MetodoPagamento.Credito,
                Valor = valorPacote,
                Parcelas = 1,
                NumeroCartao = "1111222233334444",
                Email = userEmail
            };

            var response = await _client.PostAsJsonAsync("/api/Pagamentos", pagamentoDto);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PagamentoDto>();
            Assert.NotNull(result);
            Assert.Equal("APROVADO", result.Status);
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_02_CriarPagamento_ParaReservaJaPaga_DeveRetornarBadRequest()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagamentoDto = new PagamentoEntradaDTO
            {
                ReservaId = reservaId,
                NomeCompleto = "C Teste",
                Cpf = "12345678901",
                Metodo = MetodoPagamento.Pix,
                Valor = valorPacote,
                Email = userEmail
            };

            await _client.PostAsJsonAsync("/api/Pagamentos", pagamentoDto);

            var response = await _client.PostAsJsonAsync("/api/Pagamentos", pagamentoDto);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errorBody = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Esta reserva já possui um pagamento aprovado.", errorBody.GetProperty("erro").GetString());
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_03_CriarPagamento_ComoNaoDonoDaReserva_DeveRetornarBadRequest()
        {
            // ARRANGE
            var (reservaId, valorPacote, _, _) = await CreatePackageAndReservationAsync();
            var outroClienteDto = new RegistroUsuarioDto { Nome = "Invasor", Email = "invasor@teste.com", Senha = "senha123", Documento = "98765432109", Telefone = "111" };
            await RegisterAndConfirmUserAsync(outroClienteDto);
            await LoginAndSetAuthTokenAsync(outroClienteDto.Email, outroClienteDto.Senha);

            var pagamentoDto = new PagamentoEntradaDTO
            {
                ReservaId = reservaId,
                NomeCompleto = "Tentativa Indevida",
                Cpf = "09876543210",
                Metodo = MetodoPagamento.Debito,
                Valor = valorPacote,
                Email = outroClienteDto.Email,
                NumeroCartao = "5555666677778888"
            };

            var response = await _client.PostAsJsonAsync("/api/Pagamentos", pagamentoDto);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errorBody = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Você não tem permissão para pagar por esta reserva.", errorBody.GetProperty("erro").GetString());
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_04_ObterStatus_ComoAdmin_DeveRetornarOk()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagResponse = await _client.PostAsJsonAsync("/api/Pagamentos", new PagamentoEntradaDTO { ReservaId = reservaId, NomeCompleto = "N", Cpf = "1", Metodo = MetodoPagamento.Pix, Valor = valorPacote, Email = userEmail });
            pagResponse.EnsureSuccessStatusCode();
            var pagamentoCriado = await pagResponse.Content.ReadFromJsonAsync<PagamentoDto>();
            var pagamentoId = pagamentoCriado.Id;

            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var response = await _client.GetAsync($"/api/Pagamentos/status/{pagamentoId}");

            response.EnsureSuccessStatusCode();
            var statusResult = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("APROVADO", statusResult.GetProperty("statusPagamento").GetString());
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_05_ObterStatus_ComoCliente_DeveRetornarForbidden()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagResponse = await _client.PostAsJsonAsync("/api/Pagamentos", new PagamentoEntradaDTO { ReservaId = reservaId, NomeCompleto = "N", Cpf = "1", Metodo = MetodoPagamento.Pix, Valor = valorPacote, Email = userEmail });
            pagResponse.EnsureSuccessStatusCode();
            var pagamentoId = (await pagResponse.Content.ReadFromJsonAsync<PagamentoDto>()).Id;

            var response = await _client.GetAsync($"/api/Pagamentos/status/{pagamentoId}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_06_AtualizarStatus_ComoAdmin_DeveRetornarOkEAtualizarStatus()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var pagamentoInicialDto = new PagamentoEntradaDTO
            {
                ReservaId = reservaId,
                NomeCompleto = "Cliente Para Atualizar",
                Cpf = "11122233344",
                Metodo = MetodoPagamento.Pix,
                Valor = valorPacote,
                Email = userEmail
            };
            var pagResponse = await _client.PostAsJsonAsync("/api/Pagamentos", pagamentoInicialDto);
            pagResponse.EnsureSuccessStatusCode();
            var pagamentoId = (await pagResponse.Content.ReadFromJsonAsync<PagamentoDto>()).Id;

            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var statusUpdateDto = new AtualizarStatusPagamentoDto { Status = "RECUSADO" };

            var response = await _client.PutAsJsonAsync($"/api/Pagamentos/{pagamentoId}", statusUpdateDto);

            response.EnsureSuccessStatusCode();

            var statusResponse = await _client.GetAsync($"/api/Pagamentos/status/{pagamentoId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusResult = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("RECUSADO", statusResult.GetProperty("statusPagamento").GetString());
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_07_AtualizarStatus_ComoCliente_DeveRetornarForbidden()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var pagResponse = await _client.PostAsJsonAsync("/api/Pagamentos", new PagamentoEntradaDTO { ReservaId = reservaId, NomeCompleto = "N", Cpf = "1", Metodo = MetodoPagamento.Pix, Valor = valorPacote, Email = userEmail });
            pagResponse.EnsureSuccessStatusCode();
            var pagamentoId = (await pagResponse.Content.ReadFromJsonAsync<PagamentoDto>()).Id;

            var statusUpdateDto = new AtualizarStatusPagamentoDto { Status = "APROVADO" };

            var response = await _client.PutAsJsonAsync($"/api/Pagamentos/{pagamentoId}", statusUpdateDto);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pagamentos")]
        public async Task Pag_08_CriarPagamento_ComBoleto_DeveRetornarStatusPendente()
        {
            var (reservaId, valorPacote, userEmail, userPassword) = await CreatePackageAndReservationAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var pagamentoDto = new PagamentoEntradaDTO
            {
                ReservaId = reservaId,
                NomeCompleto = "Cliente Boleto",
                Cpf = "33322211100",
                Metodo = MetodoPagamento.Boleto,
                Valor = valorPacote,
                Email = userEmail
            };

            var response = await _client.PostAsJsonAsync("/api/Pagamentos", pagamentoDto);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PagamentoDto>();
            Assert.NotNull(result);
            Assert.Equal("PENDENTE", result.Status);
        }
    }
}