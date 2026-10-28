using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LegendaLivre.Testes
{
    public class ProtocoloFonteTestes
    {
        const string ListaReal = "<subtitles><subtitle><id>MjIxMDQ4NDk=</id><name>QS5DbG9ja3dvcmsuT3JhbmdlLnNydA==</name><date>MjQtMDYtMjAxMw==</date></subtitle><subtitle><id>MjQ2MTYxMDk=</id><name>QS5DbG9ja3dvcmsuT3JhbmdlLXBvci5zcnQ=</name><date>MjctMTAtMjAxMw==</date></subtitle></subtitles>";

        [Fact]
        public void ListaXml_DecodificaCamposEMantemOrdem()
        {
            string pedido = null;
            var cliente = new ClienteLegendas(url =>
            {
                pedido = url;
                return new Resposta { Corpo = Encoding.UTF8.GetBytes(ListaReal) };
            });
            var lista = cliente.Listar("523d05d032165a93", 6);
            Assert.Equal(new[] { "22104849", "24616109" }, lista.Select(l => l.Id));
            Assert.Equal("A.Clockwork.Orange.srt", lista[0].Nome);
            Assert.Equal("24-06-2013", lista[0].Data);
            Assert.Contains("version=383&response=xml&cod_language=6&hash=NTIzZDA1ZDAzMjE2NWE5Mw%3D%3D", pedido);
        }

        [Theory]
        [InlineData("<!DOCTYPE subtitles [<!ENTITY texto SYSTEM 'file:///C:/inexistente'>]><subtitles>&texto;</subtitles>")]
        [InlineData("<html>Erro</html>")]
        [InlineData("<subtitles><subtitle>")]
        public void ListaXml_RespostaInvalidaLancaFalhaFonte(string xml) =>
            Assert.Throws<FalhaFonteException>(() => ClienteLegendas.InterpretarLista(xml));

        [Fact]
        public void ListaXml_Base64OuDataInvalidosNaoEliminamRegistrosValidos()
        {
            var invalido = "<subtitle><id>%%%invalido</id><name>QQ==</name><date>MjQtMDYtMjAxMw==</date></subtitle>";
            var xml = ListaReal.Replace("<subtitles>", "<subtitles>" + invalido + DadosFonte.Lista("A|7?31-02-2026>").Replace("<subtitles>", "").Replace("</subtitles>", ""));
            Assert.Equal(2, ClienteLegendas.InterpretarLista(xml).Count);
        }

        [Fact]
        public void Filme_UsaIdentificacaoDoHashAntesDoNomeLocal()
        {
            var video = Path.Combine(Path.GetTempPath(), "arquivo-sem-titulo-" + Guid.NewGuid() + ".mkv");
            File.WriteAllBytes(video, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            try
            {
                var pedidos = new System.Collections.Generic.List<string>();
                var filmes = new ServicoFilmes(url =>
                {
                    pedidos.Add(url);
                    return Encoding.UTF8.GetBytes(url.Contains("get_movie_by_hash")
                        ? "<movie><id>NjY5MjE=</id><name>QSBDbG9ja3dvcmsgT3JhbmdlICgxOTcxKQ==</name></movie>"
                        : "{\"metas\":[]}");
                });
                var filme = filmes.Obter(video);
                Assert.Equal("A Clockwork Orange", filme.Titulo);
                Assert.Equal("1971", filme.Ano);
                Assert.Contains("get_movie_by_hash", pedidos[0]);
                Assert.Contains("search=A%20Clockwork%20Orange", pedidos[1]);
            }
            finally { File.Delete(video); }
        }

        [Fact]
        public void ObterNomeFilme_NomeAbsurdamenteLongo_EhIgnorado()
        {
            var nome = Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('a', 301)));
            var cliente = new ClienteLegendas(url => new Resposta { Corpo = Encoding.UTF8.GetBytes("<movie><id>MQ==</id><name>" + nome + "</name></movie>") });
            Assert.Null(cliente.ObterNomeFilme("0000000000000001"));
        }

        [Theory]
        [InlineData(@"C:\v\filme.mkv", true)]
        [InlineData(@"C:\v\FILME.MP4", true)]
        [InlineData(@"C:\v\programa.exe", false)]
        [InlineData(@"C:\v\atalho.lnk", false)]
        [InlineData(@"C:\v\semextensao", false)]
        [InlineData(@"C:\v\a|b.mkv", true)]
        public void ReprodutorVideo_SoAbreExtensoesDeVideo(string caminho, bool esperado) =>
            Assert.Equal(esperado, ReprodutorVideo.ExtensaoPermitida(caminho));

        [Theory]
        [InlineData("")]
        [InlineData("<movie><id></id><name></name></movie>")]
        [InlineData("<html>Erro</html>")]
        public void Filme_FonteSemIdentificacaoAindaObtemCapaPeloNomeLocal(string respostaFonte)
        {
            var video = Path.Combine(Path.GetTempPath(), "The.Matrix.1999." + Guid.NewGuid() + ".mkv");
            File.WriteAllBytes(video, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            try
            {
                byte[] capa;
                using (var imagem = new System.Drawing.Bitmap(1, 1))
                using (var memoria = new MemoryStream())
                {
                    imagem.Save(memoria, System.Drawing.Imaging.ImageFormat.Png);
                    capa = memoria.ToArray();
                }
                var pedidos = new System.Collections.Generic.List<string>();
                var filmes = new ServicoFilmes(url =>
                {
                    pedidos.Add(url);
                    if (url.Contains("get_movie_by_hash")) return Encoding.UTF8.GetBytes(respostaFonte);
                    if (url.Contains("v3-cinemeta")) return Encoding.UTF8.GetBytes("{\"metas\":[{\"name\":\"The Matrix\",\"releaseInfo\":\"1999\",\"poster\":\"https://m.media-amazon.com/capa.png\"}]}");
                    return capa;
                });
                var filme = filmes.Obter(video);
                using (filme.Imagem)
                {
                    Assert.Equal("The Matrix", filme.Titulo);
                    Assert.Equal("1999", filme.Ano);
                    Assert.NotNull(filme.Imagem);
                    Assert.Equal(3, pedidos.Count);
                    Assert.All(pedidos, url => Assert.DoesNotContain(video, url));
                }
            }
            finally { File.Delete(video); }
        }

        [Fact]
        public void Melhor_UsaEscolhaDoServidorSemConsultarLista()
        {
            var pasta = Path.Combine(Path.GetTempPath(), "LegendaLivreProtocolo-" + Guid.NewGuid());
            Directory.CreateDirectory(pasta);
            try
            {
                var pedidos = new System.Collections.Generic.List<string>();
                var servico = new ServicoLegendas(new ClienteLegendas(url =>
                {
                    pedidos.Add(url);
                    if (url.Contains("get_best_subtitle_by_hash"))
                        return new Resposta { Corpo = Encoding.UTF8.GetBytes("<subtitle><id>MjIxMDQ4NDk=</id><date>MjQtMDYtMjAxMw==</date></subtitle>") };
                    if (url.Contains("get_subtitles_by_hash"))
                        return new Resposta { Corpo = Encoding.UTF8.GetBytes(ListaReal) };
                    using (var memoria = new MemoryStream())
                    {
                        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
                        using (var escritor = new StreamWriter(zip.CreateEntry("22104849.srt").Open())) escritor.Write("legenda");
                        return new Resposta { Corpo = memoria.ToArray() };
                    }
                }), caminho => "523d05d032165a93");
                var resultado = servico.BaixarMelhor(Path.Combine(pasta, "Filme.mkv"), Idioma.PortuguesBrasil);
                Assert.Equal(Status.Salva, resultado.Status);
                Assert.Equal(2, pedidos.Count);
                Assert.Contains("get_best_subtitle_by_hash", pedidos[0]);
                Assert.Contains("cod_bsplayer=MjIxMDQ4NDk%3D&post_date=MjQtMDYtMjAxMw%3D%3D", pedidos[1]);
                Assert.Equal("legenda", File.ReadAllText(resultado.Caminho));
            }
            finally { Directory.Delete(pasta, true); }
        }

        [Fact]
        public async Task Transporte_EnviaParametrosNoCorpoPost()
        {
            var servidor = new TcpListener(IPAddress.Loopback, 0);
            servidor.Start();
            try
            {
                int porta = ((IPEndPoint)servidor.LocalEndpoint).Port;
                var receber = servidor.AcceptTcpClientAsync();
                var obter = typeof(ClienteLegendas).GetMethod("Obter", BindingFlags.Static | BindingFlags.NonPublic);
                var chamada = Task.Run(() => obter.Invoke(null, new object[] { "http://127.0.0.1:" + porta + "/consulta.php?version=383&hash=abc%3D" }));
                Assert.Same(receber, await Task.WhenAny(receber, Task.Delay(5000)));
                string primeira;
                string cabecalhos = "";
                string corpo;
                using (var conexao = await receber)
                {
                    conexao.ReceiveTimeout = 5000;
                    using (var fluxo = conexao.GetStream())
                    {
                        var leitor = new StreamReader(fluxo, Encoding.ASCII, false, 1024, true);
                        primeira = leitor.ReadLine();
                        int tamanho = 0;
                        string linha;
                        while (!string.IsNullOrEmpty(linha = leitor.ReadLine()))
                        {
                            cabecalhos += linha + "\n";
                            if (linha.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) tamanho = int.Parse(linha.Substring(15));
                        }
                        var bytes = new char[tamanho];
                        int lidos = 0;
                        while (lidos < tamanho) lidos += leitor.Read(bytes, lidos, tamanho - lidos);
                        corpo = new string(bytes);
                        var resposta = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        fluxo.Write(resposta, 0, resposta.Length);
                    }
                }
                Assert.Same(chamada, await Task.WhenAny(chamada, Task.Delay(5000)));
                await chamada;
                Assert.Equal("POST /consulta.php HTTP/1.1", primeira);
                Assert.Contains("application/x-www-form-urlencoded", cabecalhos);
                Assert.Equal("version=383&hash=abc%3D", corpo);
            }
            finally { servidor.Stop(); }
        }
    }
}
