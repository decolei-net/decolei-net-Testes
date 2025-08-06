using Decolei.net.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Decolei.net.Tests.Testes
{
    public class AvaliacaoIntegrationTests : BaseIntegrationTests
    {
        public AvaliacaoIntegrationTests() : base() { }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl01_AvaliarPacote_ComReservaConfirmada_DeveRetornarOk()
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync(statusReserva: "CONFIRMADA");
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 5, Comentario = "Viagem incrível!" };

            var response = await _client.PostAsJsonAsync("/api/avaliacoes", avaliacaoRequest);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Avaliação registrada com sucesso.", body.GetProperty("message").GetString());
        }

        [Theory]
        [Trait("Integration", "Avaliações")]
        [InlineData(0, "Nota deve estar entre 1 e 5.")]
        [InlineData(6, "Nota deve estar entre 1 e 5.")]
        public async Task Avl02_AvaliarPacote_ComNotaInvalida_DeveRetornarBadRequestComMensagemCorreta(int notaInvalida, string mensagemEsperada)
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = notaInvalida };

            var response = await _client.PostAsJsonAsync("/api/avaliacoes", avaliacaoRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(mensagemEsperada, body);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl03_AvaliarPacote_AntesDoFimDaViagem_DeveRetornarBadRequestComMensagemCorreta()
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync(TimeSpan.FromDays(10));
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 5 };

            var response = await _client.PostAsJsonAsync("/api/avaliacoes", avaliacaoRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("Você só pode avaliar esse pacote após o término da viagem.", body);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl04_AvaliarPacote_DuasVezes_DeveRetornarBadRequestComMensagemCorreta()
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var avaliacaoRequest = new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 5 };

            await _client.PostAsJsonAsync("/api/avaliacoes", avaliacaoRequest);
            var response = await _client.PostAsJsonAsync("/api/avaliacoes", avaliacaoRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
           
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("Você já avaliou este pacote anteriormente.", body);
        }

        [Theory]
        [Trait("Integration", "Avaliações")]
        [InlineData("PENDENTE")] 
        [InlineData("CANCELADA")] 
        [InlineData(null)]      
        public async Task Avl05_AvaliarPacote_ComReservaInvalidaOuInexistente_DeveRetornarBadRequest(string statusReserva)
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync(statusReserva: statusReserva, criarReserva: statusReserva != null);
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var avaliacaoRequest = new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 4 };

            var response = await _client.PostAsJsonAsync("/api/avaliacoes", avaliacaoRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Você só pode avaliar pacotes com reservas confirmadas ou concluídas.", body.GetProperty("erro").GetString());
        }

        [Fact]
        [Trait("Integration", "Avaliações - Admin")]
        public async Task Avl06_ListarAvaliacoesPendentes_ComoAdmin_DeveRetornarLista()
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            await _client.PostAsJsonAsync("/api/avaliacoes", new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 3 });

            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var response = await _client.GetAsync("/api/avaliacoes/pendentes");

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.NotEmpty(body);
            Assert.Equal(pacoteId, body.First().GetProperty("pacoteId").GetInt32());
        }

        [Fact]
        [Trait("Integration", "Avaliações - Admin")]
        public async Task Avl07_AprovarAvaliacao_ComoAdmin_DeveFuncionar()
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            await _client.PostAsJsonAsync("/api/avaliacoes", new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 5 });

            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var pendentes = await (await _client.GetAsync("/api/avaliacoes/pendentes")).Content.ReadFromJsonAsync<List<JsonElement>>();
            var avaliacaoId = pendentes.First().GetProperty("id").GetInt32();

            var acaoDto = new AvaliacaoAcaoDto { Acao = "aprovar" };
            var response = await _client.PutAsJsonAsync($"/api/avaliacoes/{avaliacaoId}", acaoDto);
            response.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null; 
            var avaliacoesAprovadasResponse = await _client.GetAsync($"/api/avaliacoes/pacote/{pacoteId}");
            var avaliacoesAprovadas = await avaliacoesAprovadasResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.Single(avaliacoesAprovadas);
            Assert.Equal(avaliacaoId, avaliacoesAprovadas.First().GetProperty("id").GetInt32());
        }

        [Fact]
        [Trait("Integration", "Avaliações - Admin")]
        public async Task Avl08_RejeitarAvaliacao_ComoAdmin_DeveExcluir()
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            await _client.PostAsJsonAsync("/api/avaliacoes", new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 1 });

            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var pendentesAntes = await (await _client.GetAsync("/api/avaliacoes/pendentes?destino=D Teste")).Content.ReadFromJsonAsync<List<JsonElement>>();
            var avaliacaoId = pendentesAntes.First().GetProperty("id").GetInt32();

            var acaoDto = new AvaliacaoAcaoDto { Acao = "rejeitar" };
            var response = await _client.PutAsJsonAsync($"/api/avaliacoes/{avaliacaoId}", acaoDto);
            response.EnsureSuccessStatusCode();

            var pendentesDepois = await (await _client.GetAsync("/api/avaliacoes/pendentes?destino=D Teste")).Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.DoesNotContain(pendentesDepois, p => p.GetProperty("id").GetInt32() == avaliacaoId);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl09_AvaliarPacote_ComReservaConcluida_DeveRetornarOk()
        {
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync(statusReserva: "CONCLUIDA");
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new AvaliacaoRequest { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 4, Comentario = "Viagem concluída com sucesso." };

            var response = await _client.PostAsJsonAsync("/api/avaliacoes", avaliacaoRequest);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Avaliação registrada com sucesso.", body.GetProperty("message").GetString());
        }

        [Fact]
        [Trait("Integration", "Avaliações - Cliente")]
        public async Task Avl10_GetMinhasAvaliacoes_ComoCliente_DeveRetornarApenasSuasAvaliacoes()
        {
            var (usuarioId1, pacoteId1, _, email1, pass1) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(email1, pass1);
            await _client.PostAsJsonAsync("/api/avaliacoes", new AvaliacaoRequest { Usuario_Id = usuarioId1, PacoteViagem_Id = pacoteId1, Nota = 5, Comentario = "Avaliação do Cliente 1" });

            var (usuarioId2, pacoteId2, _, email2, pass2) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(email2, pass2);
            await _client.PostAsJsonAsync("/api/avaliacoes", new AvaliacaoRequest { Usuario_Id = usuarioId2, PacoteViagem_Id = pacoteId2, Nota = 3, Comentario = "Avaliação do Cliente 2" });

            await LoginAndSetAuthTokenAsync(email1, pass1);
            var response = await _client.GetAsync("/api/avaliacoes/minhas-avaliacoes");

            response.EnsureSuccessStatusCode();
            var minhasAvaliacoes = await response.Content.ReadFromJsonAsync<List<JsonElement>>();

            Assert.Single(minhasAvaliacoes); 
            Assert.Equal("Avaliação do Cliente 1", minhasAvaliacoes.First().GetProperty("comentario").GetString());
            Assert.Equal("PENDENTE", minhasAvaliacoes.First().GetProperty("status").GetString());
            Assert.Equal(pacoteId1, minhasAvaliacoes.First().GetProperty("pacote").GetProperty("id").GetInt32());
        }

        [Fact]
        [Trait("Integration", "Avaliações - Admin")]
        public async Task Avl11_ListarAvaliacoesAprovadas_ComoAdmin_DeveRetornarApenasAprovadas()
        {
            var (usuarioId1, pacoteId1, _, email1, pass1) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(email1, pass1);
            await _client.PostAsJsonAsync("/api/avaliacoes", new AvaliacaoRequest { Usuario_Id = usuarioId1, PacoteViagem_Id = pacoteId1, Nota = 5 }); // Esta será aprovada

            var (usuarioId2, _, _, email2, pass2) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(email2, pass2);
            await _client.PostAsJsonAsync("/api/avaliacoes", new AvaliacaoRequest { Usuario_Id = usuarioId2, PacoteViagem_Id = pacoteId1, Nota = 2 }); // Esta ficará pendente

            // Aprova a primeira avaliação
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var pendentes = await (await _client.GetAsync("/api/avaliacoes/pendentes")).Content.ReadFromJsonAsync<List<JsonElement>>();
            var avaliacaoParaAprovarId = pendentes.First(p => p.GetProperty("usuarioId").GetInt32() == usuarioId1).GetProperty("id").GetInt32();
            await _client.PutAsJsonAsync($"/api/avaliacoes/{avaliacaoParaAprovarId}", new AvaliacaoAcaoDto { Acao = "aprovar" });

            var response = await _client.GetAsync("/api/avaliacoes/aprovadas");

            response.EnsureSuccessStatusCode();
            var aprovadas = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.Single(aprovadas);
            Assert.Equal(avaliacaoParaAprovarId, aprovadas.First().GetProperty("id").GetInt32());
        }
    }
}