using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace LegendaLivre.Testes
{
    public class ServicoLegendasTestes : IDisposable
    {
        readonly string _pasta = Path.Combine(Path.GetTempPath(), "LegendaLivreServ-" + Guid.NewGuid());
        readonly string _video;

        public ServicoLegendasTestes()
        {
            Directory.CreateDirectory(_pasta);
            _video = Path.Combine(_pasta, "Filme.mkv");
            File.WriteAllBytes(_video, new byte[16]);
        }

        public void Dispose() => Directory.Delete(_pasta, true);

        static byte[] ZipCom(string nome, string conteudo)
        {
            using (var mem = new MemoryStream())
            {
                using (var zip = new ZipArchive(mem, ZipArchiveMode.Create, true))
                using (var s = zip.CreateEntry(nome).Open())
                {
                    var b = Encoding.ASCII.GetBytes(conteudo);
                    s.Write(b, 0, b.Length);
                }
                return mem.ToArray();
            }
        }

        static ServicoLegendas Servico(string lista, byte[] zip = null, string hash = "a4de2549514922e2") =>
            new ServicoLegendas(new ClienteLegendas(url =>
                url.Contains("by_hash")
                    ? new Resposta { Corpo = Encoding.UTF8.GetBytes(DadosFonte.Lista(lista, url.Contains("get_best"))), TipoConteudo = "text/xml" }
                    : new Resposta { Corpo = zip ?? new byte[0], TipoConteudo = "application/octet-stream" }),
                caminho => hash);

        [Fact]
        public void BaixarMelhor_UsaRespostaDoServidorESalva()
        {
            string idBaixado = null;
            var servico = new ServicoLegendas(new ClienteLegendas(url =>
            {
                if (url.Contains("get_best_subtitle_by_hash"))
                    return new Resposta { Corpo = Encoding.UTF8.GetBytes(DadosFonte.Lista("A|1?01-01-2020>", true)), TipoConteudo = "text/xml" };
                idBaixado = url.Contains("cod_bsplayer=MQ%3D%3D&") ? "1" : "outro";
                return new Resposta { Corpo = ZipCom("1.srt", "ola"), TipoConteudo = "application/octet-stream" };
            }), c => "a4de2549514922e2");

            var r = servico.BaixarMelhor(_video, Idioma.PortuguesBrasil);

            Assert.Equal(Status.Salva, r.Status);
            Assert.Equal("1", idBaixado);
            Assert.Equal(Path.Combine(_pasta, "Filme.srt"), r.Caminho);
            Assert.Equal("ola", File.ReadAllText(r.Caminho));
        }

        [Fact]
        public void BaixarMelhor_ListaVazia_NaoEncontrada() =>
            Assert.Equal(Status.NaoEncontrada, Servico("").BaixarMelhor(_video, Idioma.Ingles).Status);

        [Fact]
        public void BaixarMelhor_HashZerado_NaoEncontradaSemConsultar()
        {
            bool consultou = false;
            var servico = new ServicoLegendas(new ClienteLegendas(url => { consultou = true; return null; }), c => "0000000000000000");
            Assert.Equal(Status.NaoEncontrada, servico.BaixarMelhor(_video, Idioma.PortuguesBrasil).Status);
            Assert.False(consultou);
        }

        [Fact]
        public void BaixarMelhor_VideoSumiu_FalhaLeitura()
        {
            var servico = new ServicoLegendas(new ClienteLegendas(url => null), ImpressaoDigital.Calcular);
            Assert.Equal(Status.FalhaLeitura, servico.BaixarMelhor(Path.Combine(_pasta, "sumiu.mkv"), Idioma.PortuguesBrasil).Status);
        }

        [Fact]
        public void BaixarMelhor_SemRede_FalhaLista()
        {
            var servico = new ServicoLegendas(new ClienteLegendas(url => throw new FalhaFonteException("x")), c => "a4de2549514922e2");
            Assert.Equal(Status.FalhaLista, servico.BaixarMelhor(_video, Idioma.PortuguesBrasil).Status);
        }

        [Fact]
        public void BaixarMelhor_RespostaHtmlNoDownload_FalhaDownload() =>
            Assert.Equal(Status.FalhaDownload, Servico("A|1?01-01-2020>", Encoding.ASCII.GetBytes("<html>")).BaixarMelhor(_video, Idioma.PortuguesBrasil).Status);

        [Fact]
        public void BaixarMelhor_ZipSemLegenda_FalhaDownload() =>
            Assert.Equal(Status.FalhaDownload, Servico("A|1?01-01-2020>", ZipCom("leia.txt", "x")).BaixarMelhor(_video, Idioma.PortuguesBrasil).Status);

        [Fact]
        public void BaixarMelhor_PastaSemPermissao_FalhaGravacao()
        {
            var videoSemPasta = Path.Combine(_pasta, "nao-existe", "Filme.mkv");
            var servico = new ServicoLegendas(new ClienteLegendas(url =>
                url.Contains("get_best_subtitle_by_hash")
                    ? new Resposta { Corpo = Encoding.UTF8.GetBytes(DadosFonte.Lista("A|1?01-01-2020>", true)), TipoConteudo = "text/xml" }
                    : new Resposta { Corpo = ZipCom("1.srt", "x"), TipoConteudo = "application/octet-stream" }), c => "a4de2549514922e2");
            Assert.Equal(Status.FalhaGravacao, servico.BaixarMelhor(videoSemPasta, Idioma.PortuguesBrasil).Status);
        }

        [Fact]
        public void BaixarMelhor_CaminhoComDoisPontos_FalhaLeitura()
        {
            var servico = new ServicoLegendas(new ClienteLegendas(url => null), ImpressaoDigital.Calcular);
            Assert.Equal(Status.FalhaLeitura, servico.BaixarMelhor(Path.Combine(_pasta, "a:b.mkv"), Idioma.PortuguesBrasil).Status);
        }

        [Fact]
        public void Baixar_CaminhoSemPasta_FalhaGravacao()
        {
            var servico = Servico("", ZipCom("1.srt", "x"));
            Assert.Equal(Status.FalhaGravacao, servico.Baixar("C:\\", new Legenda { Nome = "A", Id = "1" }).Status);
        }

        [Fact]
        public void Listar_HashZerado_NaoEncontradaSemConsultar()
        {
            bool consultou = false;
            var servico = new ServicoLegendas(new ClienteLegendas(url => { consultou = true; return null; }), c => "0000000000000000");
            Assert.Equal(Status.NaoEncontrada, servico.Listar(_video, Idioma.PortuguesBrasil, 10).Status);
            Assert.False(consultou);
        }

        [Fact]
        public void Listar_LimitaAoMaximo()
        {
            var lista = new StringBuilder();
            for (int i = 1; i <= 15; i++) lista.Append($"L{i}|{i}?01-01-2020>");
            var r = Servico(lista.ToString()).Listar(_video, Idioma.PortuguesBrasil, 10);
            Assert.Equal(Status.Salva, r.Status);
            Assert.Equal(10, r.Legendas.Count);
            Assert.Equal("L1", r.Legendas[0].Nome);
        }

        [Fact]
        public void Listar_Vazia_NaoEncontrada() =>
            Assert.Equal(Status.NaoEncontrada, Servico("").Listar(_video, Idioma.PortuguesBrasil, 10).Status);

        [Theory]
        [InlineData(Status.NaoEncontrada, Mensagens.NenhumaLegenda)]
        [InlineData(Status.FalhaLista, Mensagens.FalhaLista)]
        [InlineData(Status.FalhaDownload, Mensagens.FalhaDownload)]
        [InlineData(Status.FalhaGravacao, Mensagens.FalhaGravacao)]
        [InlineData(Status.FalhaLeitura, Mensagens.FalhaLeitura)]
        [InlineData(Status.Salva, Mensagens.Sucesso)]
        public void MensagemPara(Status status, string esperado) => Assert.Equal(esperado, ServicoLegendas.MensagemPara(status));
    }
}
