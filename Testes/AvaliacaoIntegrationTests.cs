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

        // Teste para o cenário de sucesso ao criar uma avaliação.
        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl01_AvaliarPacote_ComDadosValidos_DeveRetornarOk()
        {
            // ARRANGE: Cria um cenário completo: usuário, pacote, e uma reserva CONFIRMADA que já terminou.
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 5, Comentario = "Viagem incrível!" };

            // ACT: Envia a requisição para avaliar o pacote.
            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            // ASSERT: Verifica se a operação foi bem-sucedida.
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Avaliação registrada com sucesso.", body.GetProperty("message").GetString());
        }

        // Testa se o controller rejeita notas fora do intervalo permitido (1-5).
        [Theory]
        [Trait("Integration", "Avaliações")]
        [InlineData(0)]
        [InlineData(6)]
        public async Task Avl02_AvaliarPacote_ComNotaInvalida_DeveRetornarBadRequest(int notaInvalida)
        {
            // ARRANGE
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = notaInvalida };

            // ACT
            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            // ASSERT
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Testa a regra de negócio que só permite avaliar após o fim da data da viagem.
        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl03_AvaliarPacote_AntesDoFimDaViagem_DeveRetornarBadRequest()
        {
            // ARRANGE: Cria um cenário onde a viagem só termina daqui a 10 dias.
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync(TimeSpan.FromDays(10));
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);

            var avaliacaoRequest = new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 5 };

            // ACT
            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            // ASSERT
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Testa a validação que impede um usuário de avaliar o mesmo pacote duas vezes.
        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl04_AvaliarPacote_DuasVezes_DeveRetornarBadRequest()
        {
            // ARRANGE
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            var avaliacaoRequest = new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 5 };

            // ACT: Faz a primeira avaliação (que deve funcionar) e a segunda (que deve falhar).
            await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);
            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            // ASSERT
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Testa se o usuário precisa ter uma reserva confirmada para poder avaliar.
        [Fact]
        [Trait("Integration", "Avaliações")]
        public async Task Avl05_AvaliarPacote_SemReservaConfirmada_DeveRetornarBadRequest()
        {
            // ARRANGE: Cria um usuário e um pacote, mas não cria uma reserva.
            var userDto = new RegistroUsuarioDto { Nome = "Avaliador Sem Reserva", Email = "semreserva@teste.com", Senha = "senha123", Documento = "12312312312" };
            await RegisterAndConfirmUserAsync(userDto);
            var userId = (await GetUserIdByEmail(userDto.Email)).Value;

            var pacoteId = await CreatePackageAndGetIdAsync(new CriarPacoteViagemDto { Titulo = "P", Destino = "D", Valor = 1, DataInicio = DateTime.UtcNow.AddDays(-2), DataFim = DateTime.UtcNow.AddDays(-1) });
            await LoginAndSetAuthTokenAsync(userDto.Email, userDto.Senha);
            var avaliacaoRequest = new { Usuario_Id = userId, PacoteViagem_Id = pacoteId, Nota = 4 };

            // ACT
            var response = await _client.PostAsJsonAsync("/avaliacoes", avaliacaoRequest);

            // ASSERT
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Testa se o admin consegue ver a lista de avaliações pendentes.
        [Fact]
        [Trait("Integration", "Avaliações - Admin")]
        public async Task Avl06_ListarAvaliacoesPendentes_ComoAdmin_DeveRetornarLista()
        {
            // ARRANGE: Cria uma avaliação, que por padrão fica pendente.
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            await _client.PostAsJsonAsync("/avaliacoes", new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 3 });

            // ACT: Loga como admin e busca as avaliações pendentes.
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var response = await _client.GetAsync("/avaliacoes/pendentes");

            // ASSERT
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.NotEmpty(body);
        }

        // Testa o fluxo completo de aprovação de uma avaliação por um admin.
        [Fact]
        [Trait("Integration", "Avaliações - Admin")]
        public async Task Avl07_AprovarAvaliacao_ComoAdmin_DeveFuncionar()
        {
            // ARRANGE: Cria uma avaliação pendente.
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            await _client.PostAsJsonAsync("/avaliacoes", new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 5 });

            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            // Pega o ID da avaliação que acabou de ser criada.
            var pendentes = await (await _client.GetAsync("/avaliacoes/pendentes")).Content.ReadFromJsonAsync<List<JsonElement>>();
            var avaliacaoId = pendentes.First().GetProperty("id").GetInt32();

            // ACT: Aprova a avaliação.
            var acaoDto = new { Acao = "aprovar" };
            var response = await _client.PutAsJsonAsync($"/avaliacoes/{avaliacaoId}", acaoDto);
            response.EnsureSuccessStatusCode();

            // ASSERT: Verifica se a avaliação agora aparece na lista pública do pacote.
            _client.DefaultRequestHeaders.Authorization = null; // Desloga para testar como usuário público.
            var avaliacoesAprovadasResponse = await _client.GetAsync($"/avaliacoes/pacote/{pacoteId}");
            var avaliacoesAprovadas = await avaliacoesAprovadasResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.Single(avaliacoesAprovadas);
        }

        // Testa o fluxo de rejeição e exclusão de uma avaliação por um admin.
        [Fact]
        [Trait("Integration", "Avaliações - Admin")]
        public async Task Avl08_RejeitarAvaliacao_ComoAdmin_DeveExcluir()
        {
            // ARRANGE: Cria uma avaliação pendente.
            var (usuarioId, pacoteId, _, userEmail, userPassword) = await CreateValidScenarioForReviewAsync();
            await LoginAndSetAuthTokenAsync(userEmail, userPassword);
            await _client.PostAsJsonAsync("/avaliacoes", new { Usuario_Id = usuarioId, PacoteViagem_Id = pacoteId, Nota = 1 });

            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var pendentesAntes = await (await _client.GetAsync("/avaliacoes/pendentes")).Content.ReadFromJsonAsync<List<JsonElement>>();
            var avaliacaoId = pendentesAntes.First().GetProperty("id").GetInt32();

            // ACT: Rejeita (exclui) a avaliação.
            var acaoDto = new { Acao = "rejeitar" };
            var response = await _client.PutAsJsonAsync($"/avaliacoes/{avaliacaoId}", acaoDto);
            response.EnsureSuccessStatusCode();

            // ASSERT: Verifica se a lista de pendentes agora está vazia.
            var pendentesDepois = await (await _client.GetAsync("/avaliacoes/pendentes")).Content.ReadFromJsonAsync<List<JsonElement>>();
            Assert.Empty(pendentesDepois);
        }
    }
}