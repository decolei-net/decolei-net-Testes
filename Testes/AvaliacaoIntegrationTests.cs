using Decolei.net.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Decolei.net.Tests;

namespace Decolei.net.Tests.Testes
{
    public class AvaliacaoIntegrationTests : BaseIntegrationTests
    {
        public AvaliacaoIntegrationTests() : base() { }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl01_AvaliarPacote_ComDadosValidos_DeveRetornarOk()
        {
            var (usuarioId, pacoteId, reservaId, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Reserva_Id = reservaId, Nota = 5, Comentario = "Viagem incrível!" };

            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [Trait("Integration", "Avaliações")]
        [InlineData(0)]
        [InlineData(6)]
        public async Task Avl02_AvaliarPacote_ComNotaInvalida_DeveRetornarBadRequest(int notaInvalida)
        {
            var (usuarioId, pacoteId, reservaId, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Reserva_Id = reservaId, Nota = notaInvalida };

            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl03_AvaliarPacote_AntesDoFimDaViagem_DeveRetornarBadRequest()
        {
            var (usuarioId, pacoteId, reservaId, userEmail, userPassword) = await CreateValidScenarioForReviewAsync(TimeSpan.FromDays(10)); // Viagem termina em 10 dias
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Reserva_Id = reservaId, Nota = 5 };

            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl04_AvaliarPacote_DuasVezes_DeveRetornarBadRequest()
        {
            var (usuarioId, pacoteId, reservaId, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var avaliacaoRequest = new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Reserva_Id = reservaId, Nota = 5 };

            await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);
            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl05_AvaliarPacote_SemReservaConfirmada_DeveRetornarBadRequest()
        {
            var userDto = new RegistroUsuarioDto
            {
                Nome = "Sem Reserva",
                Email = "semreserva@teste.com",
                Senha = "SenhaValida123", 
                Documento = "11122233344",
                Telefone = "81992345678" 
            };
            await RegisterAndConfirmUserAsync(userDto);
            
            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P", Destino = "D", Valor = 1, DataInicio = DateTime.UtcNow.AddDays(-2), DataFim = DateTime.UtcNow.AddDays(-1) });
            var userId = (await GetUserIdByEmail("semreserva@teste.com")).Value;
            await LoginAndSetAuthTokenAsync("semreserva@teste.com", userDto.Senha);

            var avaliacaoRequest = new { Usuario_Id = userId, PacoteViagem_Id = pacoteId, Reserva_Id = 999, Nota = 4 };
            
            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl06_ListarAvaliacoesPendentes_ComoAdmin_DeveRetornarLista()
        {
            var (usuarioId, pacoteId, reservaId, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            await _client.PostAsJsonAsync("/avaliacoes", new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Reserva_Id = reservaId, Nota = 3 });

            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var response = await _client.GetAsync("/avaliacoes/pendentes");

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.NotEmpty(body);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl07_AprovarAvaliacao_ComoAdmin_DeveFuncionar()
        {
            var (usuarioId, pacoteId, reservaId, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var responseCriacao = await _client.PostAsJsonAsync("/avaliacoes", new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Reserva_Id = reservaId, Nota = 5 });

            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            // Precisamos do ID da avaliação criada. Para isso, buscamos as pendentes.
            var pendentes = await (await _client.GetAsync("/avaliacoes/pendentes")).Content.ReadFromJsonAsync<List<JsonElement>>();
            var avaliacaoId = pendentes.First().GetProperty("id").GetInt32();

            var acaoDto = new { Acao = "aprovar" };
            var response = await _client.PutAsJsonAsync($"/avaliacoes/{avaliacaoId}", acaoDto);

            response.EnsureSuccessStatusCode();

            // Verificação final: a avaliação aprovada deve aparecer na lista pública do pacote.
            var avaliacoesAprovadas = await (await _client.GetAsync($"/avaliacoes/pacote/{pacoteId}")).Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.Single(avaliacoesAprovadas);
        }

        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl08_RejeitarAvaliacao_ComoAdmin_DeveExcluir()
        {
            var (usuarioId, pacoteId, reservaId, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            await _client.PostAsJsonAsync("/avaliacoes", new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Reserva_Id = reservaId, Nota = 1 });

            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var pendentes = await (await _client.GetAsync("/avaliacoes/pendentes")).Content.ReadFromJsonAsync<List<JsonElement>>();
            var avaliacaoId = pendentes.First().GetProperty("id").GetInt32();

            var acaoDto = new { Acao = "rejeitar" };
            var response = await _client.PutAsJsonAsync($"/avaliacoes/{avaliacaoId}", acaoDto);
            response.EnsureSuccessStatusCode();

            // Verificação final: a lista de pendentes agora deve estar vazia.
            pendentes = await (await _client.GetAsync("/avaliacoes/pendentes")).Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.Empty(pendentes);
        }
    }
}