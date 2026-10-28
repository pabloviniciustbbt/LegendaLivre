using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Xunit;

namespace LegendaLivre.Testes
{
    public class TelaLegendasTestes
    {
        [Fact]
        public void Alternativas_PreservamArquivosExistentes()
        {
            var pasta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados-testes", "LegendaLivre.Teste." + Guid.NewGuid());
            Directory.CreateDirectory(pasta);
            try
            {
                var video = Path.Combine(pasta, "Filme.mkv");
                var existente = Path.Combine(pasta, "Filme.legenda-2.srt");
                File.WriteAllText(existente, "preservar");
                var legenda = new ArquivoLegenda { Extensao = ".srt", Bytes = Encoding.UTF8.GetBytes("nova") };
                var principal = Salvamento.Gravar(video, legenda);
                var segunda = Salvamento.Gravar(video, legenda, true);
                var terceira = Salvamento.Gravar(video, legenda, true);
                Assert.Equal(Path.Combine(pasta, "Filme.srt"), principal);
                Assert.Equal(Path.Combine(pasta, "Filme.legenda-3.srt"), segunda);
                Assert.Equal(Path.Combine(pasta, "Filme.legenda-4.srt"), terceira);
                Assert.Equal("preservar", File.ReadAllText(existente));
                Assert.Equal("nova", File.ReadAllText(segunda));
            }
            finally { Directory.Delete(pasta, true); }
        }

        [Fact]
        public void Cancelamento_ImpedeIniciarDownload()
        {
            var chamadas = 0;
            var cliente = new ClienteLegendas(url => { chamadas++; return new Resposta(); });
            var servico = new ServicoLegendas(cliente, video => "123");
            using (var cancelamento = new CancellationTokenSource())
            {
                cancelamento.Cancel();
                Assert.Throws<OperationCanceledException>(() => servico.Baixar("Filme.mkv", new Legenda(), false, cancelamento.Token));
                Assert.Equal(0, chamadas);
            }
        }

        [Theory]
        [InlineData("The.Matrix.1999.1080p.BluRay.mkv", "The Matrix", "1999")]
        [InlineData("Filme Exemplo (2024).mp4", "Filme Exemplo", "2024")]
        [InlineData("1984.1984.720p.mkv", "1984", "1984")]
        [InlineData("Serie.Exemplo.S01E02.1080p.mkv", "Serie Exemplo", null)]
        public void Filme_ExtraiTituloEAno(string arquivo, string titulo, string ano)
        {
            var filme = ServicoFilmes.Identificar(arquivo);
            Assert.Equal(titulo, filme.Titulo);
            Assert.Equal(ano, filme.Ano);
        }

        [Fact]
        public void Filme_FalhaDeRedeNaoImpedeDadosLocais()
        {
            var servico = new ServicoFilmes(url => throw new IOException("sem rede"));
            var filme = servico.Obter("The.Matrix.1999.mkv");
            Assert.Equal("The Matrix", filme.Titulo);
            Assert.Equal("1999", filme.Ano);
            Assert.Null(filme.Imagem);
        }

        [Fact]
        public void Filme_NaoUsaCapaDeAnoDiferente()
        {
            var chamadas = 0;
            var servico = new ServicoFilmes(url => { chamadas++; return Encoding.UTF8.GetBytes("{\"metas\":[{\"name\":\"The Matrix\",\"releaseInfo\":\"2021\",\"poster\":\"https://exemplo.invalid/capa.jpg\"}]}"); });
            var filme = servico.Obter("The.Matrix.1999.mkv");
            Assert.Null(filme.Imagem);
            Assert.Equal(1, chamadas);
        }

        [Fact]
        public void Filme_NaoEscolheCapaAmbigua()
        {
            var chamadas = 0;
            var servico = new ServicoFilmes(url =>
            {
                chamadas++;
                return Encoding.UTF8.GetBytes("{\"metas\":[{\"name\":\"The Matrix\",\"releaseInfo\":\"1999\"},{\"name\":\"The Matrix\",\"releaseInfo\":\"1999\"}]}");
            });
            Assert.Null(servico.Obter("The.Matrix.1999.mkv").Imagem);
            Assert.Equal(1, chamadas);
        }

        [Fact]
        public void Cancelamento_DuranteRedeImpedeGravacao()
        {
            using (var cancelamento = new CancellationTokenSource())
            using (var memoria = new MemoryStream())
            {
                using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
                using (var escritor = new StreamWriter(zip.CreateEntry("legenda.srt").Open())) escritor.Write("legenda");
                var video = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cancelada-" + Guid.NewGuid() + ".mkv");
                var cliente = new ClienteLegendas(url =>
                {
                    cancelamento.Cancel();
                    return new Resposta { Corpo = memoria.ToArray() };
                });
                var servico = new ServicoLegendas(cliente, arquivo => "123");
                Assert.Throws<OperationCanceledException>(() => servico.Baixar(video, new Legenda { Id = "1", Data = "01-01-2020" }, false, cancelamento.Token));
                Assert.False(File.Exists(Path.ChangeExtension(video, ".srt")));
            }
        }
    }
}
