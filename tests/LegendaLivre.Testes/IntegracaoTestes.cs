using System;
using System.IO;
using Xunit;

namespace LegendaLivre.Testes
{
    // Só roda com a variável LEGENDALIVRE_VIDEO_TESTE apontando para um vídeo real.
    // A legenda é gravada numa cópia esparsa do vídeo (mesmo tamanho, cabeça e cauda), nunca ao lado do original.
    public class IntegracaoTestes
    {
        static string VideoReal => Environment.GetEnvironmentVariable("LEGENDALIVRE_VIDEO_TESTE");

        [Fact]
        public void ServidorReal_ListaNosTresIdiomasEBaixa()
        {
            if (string.IsNullOrEmpty(VideoReal)) return;
            var hash = ImpressaoDigital.Calcular(VideoReal);
            var cliente = new ClienteLegendas();
            foreach (var idioma in Idioma.Todos)
                Assert.NotEmpty(cliente.Listar(hash, idioma.Codigo));

            var pasta = Path.Combine(Path.GetTempPath(), "LegendaLivreInteg-" + Guid.NewGuid());
            Directory.CreateDirectory(pasta);
            try
            {
                var copia = Path.Combine(pasta, Path.GetFileName(VideoReal));
                CopiaEsparsa(VideoReal, copia);
                Assert.Equal(hash, ImpressaoDigital.Calcular(copia));
                var r = new ServicoLegendas().BaixarMelhor(copia, Idioma.PortuguesBrasil);
                Assert.Equal(Status.Salva, r.Status);
                Assert.True(new FileInfo(r.Caminho).Length > 1000);
            }
            finally { Directory.Delete(pasta, true); }
        }

        static void CopiaEsparsa(string origem, string destino)
        {
            const int bloco = 65536;
            using (var o = File.OpenRead(origem))
            using (var d = File.Create(destino))
            {
                var buf = new byte[bloco];
                int n = o.Read(buf, 0, bloco);
                d.Write(buf, 0, n);
                d.SetLength(o.Length);
                o.Position = Math.Max(0, o.Length - bloco);
                d.Position = o.Position;
                n = o.Read(buf, 0, bloco);
                d.Write(buf, 0, n);
            }
        }
    }
}
