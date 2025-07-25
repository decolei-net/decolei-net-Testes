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
        [Trait("Integration", "Pacotes - CRUD")]
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
        [Trait("Integration", "Pacotes - Segurança")]
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
        [Trait("Integration", "Pacotes - Segurança")]
        public async Task Pkg03_TentarCriarPacote_SemAutenticacao_DeveRetornarUnauthorized()
        {
            _client.DefaultRequestHeaders.Authorization = null;
            var pacoteDto = new CriarPacoteViagemDto { Titulo = "Teste Sem Auth", Destino = "Falha", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) };
            var response = await _client.PostAsJsonAsync(PacotesEndpoint, pacoteDto);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes - CRUD")]
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
        [Trait("Integration", "Pacotes - CRUD")]
        public async Task Pkg05_TentarCriarPacote_ComDadosInvalidos_DeveRetornarBadRequest()
        {
            await EnsureAdminUserExistsAsync();
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var pacoteInvalido = new { Destino = "Marte", Valor = 150000m };
            var response = await _client.PostAsJsonAsync(PacotesEndpoint, pacoteInvalido);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes - CRUD")]
        public async Task Pkg06_ObterPacotePorId_Existente_DeveRetornarOk()
        {
            var pacoteDto = new CriarPacoteViagemDto { Titulo = "Pacote de Teste", Destino = "Terra", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) };
            var novoPacoteId = await CreateAndGetPackageIdAsync(pacoteDto);

            var response = await _client.GetAsync($"{PacotesEndpoint}/{novoPacoteId}");

            response.EnsureSuccessStatusCode();
            var pacoteRetornado = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(novoPacoteId, pacoteRetornado.GetProperty("id").GetInt32());
            Assert.Equal("Pacote de Teste", pacoteRetornado.GetProperty("titulo").GetString());
        }

        [Fact]
        [Trait("Integration", "Pacotes - CRUD")]
        public async Task Pkg07_ObterPacotePorId_Inexistente_DeveRetornarNotFound()
        {
            var response = await _client.GetAsync($"{PacotesEndpoint}/9999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes - CRUD")]
        public async Task Pkg08_AtualizarPacote_ComoAdmin_DeveRetornarOk()
        {
            var pacoteId = await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Título Antigo", Destino = "Terra", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);
            var updateDto = new UpdatePacoteViagemDto { Titulo = "Título Novo e Atualizado" };

            var response = await _client.PutAsJsonAsync($"{PacotesEndpoint}/{pacoteId}", updateDto);

            response.EnsureSuccessStatusCode();

            var getResponse = await _client.GetAsync($"{PacotesEndpoint}/{pacoteId}");
            var pacoteAtualizado = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Título Novo e Atualizado", pacoteAtualizado.GetProperty("titulo").GetString());
        }

        [Fact]
        [Trait("Integration", "Pacotes - Segurança")]
        public async Task Pkg09_AtualizarPacote_ComoCliente_DeveRetornarForbidden()
        {
            var pacoteId = await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Residência", Destino = "Casa", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var clienteDto = new RegistroUsuarioDto { Email = "cliente.curioso@teste.com", Nome = "Curioso", Senha = "SenhaValida1", Documento = "111", Telefone = "111" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var updateDto = new UpdatePacoteViagemDto { Titulo = "Tentei Mudar" };
            var response = await _client.PutAsJsonAsync($"{PacotesEndpoint}/{pacoteId}", updateDto);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes - Segurança")]
        public async Task Pkg10_AtualizarPacote_SemAutenticacao_DeveRetornarUnauthorized()
        {
            var pacoteId = await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Pacote Criado", Destino = "Teste", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });

            _client.DefaultRequestHeaders.Authorization = null;

            var updateDto = new UpdatePacoteViagemDto { Titulo = "Tentativa Anônima" };

            var response = await _client.PutAsJsonAsync($"{PacotesEndpoint}/{pacoteId}", updateDto);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes - CRUD")]
        public async Task Pkg11_RemoverPacote_ComoAdmin_DeveRetornarOk()
        {
            var pacoteId = await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Pacote a ser Deletado", Destino = "Vazio", Valor = 1, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            await LoginAndSetAuthTokenAsync(AdminEmail, AdminPassword);

            var deleteResponse = await _client.DeleteAsync($"{PacotesEndpoint}/{pacoteId}");

            deleteResponse.EnsureSuccessStatusCode();

            var getResponse = await _client.GetAsync($"{PacotesEndpoint}/{pacoteId}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes - Segurança")]
        public async Task Pkg12_RemoverPacote_ComoCliente_DeveRetornarForbidden()
        {
            var pacoteId = await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Pernambuco espetacular", Destino = "Recife", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            var clienteDto = new RegistroUsuarioDto { Email = "cliente@teste.com", Nome = "DestruidorDePacotes", Senha = "SenhaValida2", Documento = "222", Telefone = "222" };
            await RegisterAndConfirmUserAsync(clienteDto);
            await LoginAndSetAuthTokenAsync(clienteDto.Email, clienteDto.Senha);

            var response = await _client.DeleteAsync($"{PacotesEndpoint}/{pacoteId}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        [Trait("Integration", "Pacotes - Filtros")]
        public async Task Pkg13_FiltrarPacotes_PorDestino_DeveRetornarApenasCorrespondentes()
        {
            await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Viagem para a Lua", Destino = "Lua", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Férias em Marte", Destino = "Marte", Valor = 200, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Lua de Mel", Destino = "Lua", Valor = 300, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });

            var response = await _client.GetAsync($"{PacotesEndpoint}?destino=Lua");

            response.EnsureSuccessStatusCode();
            var pacotes = await ExtractListFromResponse(response);
            Assert.Equal(2, pacotes.Count);
            Assert.True(pacotes.All(p => p.GetProperty("destino").GetString() == "Lua"));
        }

        [Fact]
        [Trait("Integration", "Pacotes - Filtros")]
        public async Task Pkg14_FiltrarPacotes_PorCombinacaoDePreco_DeveFuncionar()
        {
            await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Barato", Destino = "A", Valor = 1000, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Médio", Destino = "B", Valor = 2500, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });
            await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Caro", Destino = "C", Valor = 5000, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });

            var response = await _client.GetAsync($"{PacotesEndpoint}?precoMin=2000&precoMax=3000");

            response.EnsureSuccessStatusCode();
            var pacotes = await ExtractListFromResponse(response);
            Assert.Single(pacotes);
            Assert.Equal(2500, pacotes.First().GetProperty("valor").GetDecimal());
        }

        [Fact]
        [Trait("Integration", "Pacotes - Filtros")]
        public async Task Pkg15_FiltrarPacotes_SemResultados_DeveRetornarListaVazia()
        {
            await CreateAndGetPackageIdAsync(new CriarPacoteViagemDto { Titulo = "Pacote Real", Destino = "Terra", Valor = 100, DataInicio = DateTime.Now, DataFim = DateTime.Now.AddDays(1) });

            var response = await _client.GetAsync($"{PacotesEndpoint}?destino=Plutao");

            response.EnsureSuccessStatusCode();
            var pacotes = await ExtractListFromResponse(response);
            Assert.Empty(pacotes);
        }
    }
}