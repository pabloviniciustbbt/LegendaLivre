using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace LegendaLivre.Testes
{
    public class SalvamentoTestes : IDisposable
    {
        readonly string _pasta = Path.Combine(Path.GetTempPath(), "LegendaLivreSalv-" + Guid.NewGuid());
        static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

        public SalvamentoTestes() => Directory.CreateDirectory(_pasta);
        public void Dispose()
        {
            foreach (var f in Directory.GetFiles(_pasta)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(_pasta, true);
        }

        [Fact]
        public void Substituir_FalhaDoReplace_DestinoFicaComConteudoNovo()
        {
            var destino = Path.Combine(_pasta, "a.srt");
            var tmp = destino + ".tmp";
            File.WriteAllText(destino, "velha");
            File.WriteAllText(tmp, "nova");
            Salvamento.Substituir(tmp, destino, (t, d) => throw new IOException("falha simulada"));
            Assert.Equal("nova", File.ReadAllText(destino));
            Assert.False(File.Exists(tmp));
        }

        [Fact]
        public void Substituir_FalhaDoReplaceComDestinoAusente_MoveTemporario()
        {
            var destino = Path.Combine(_pasta, "b.srt");
            var tmp = destino + ".tmp";
            File.WriteAllText(tmp, "nova");
            Salvamento.Substituir(tmp, destino, (t, d) => throw new IOException("falha simulada"));
            Assert.Equal("nova", File.ReadAllText(destino));
            Assert.False(File.Exists(tmp));
        }

        static byte[] Zip(params (string nome, byte[] dados)[] entradas)
        {
            using (var mem = new MemoryStream())
            {
                using (var zip = new ZipArchive(mem, ZipArchiveMode.Create, true))
                    foreach (var (nome, dados) in entradas)
                        using (var s = zip.CreateEntry(nome).Open()) s.Write(dados, 0, dados.Length);
                return mem.ToArray();
            }
        }

        [Fact]
        public void ExtrairDoZip_UmaLegenda()
        {
            var r = Salvamento.ExtrairDoZip(Zip(("71499183.srt", new byte[] { 1, 2, 3 })));
            Assert.Equal(".srt", r.Extensao);
            Assert.Equal(new byte[] { 1, 2, 3 }, r.Bytes);
        }

        [Fact]
        public void ExtrairDoZip_NomeDeEntradaInvalido_LancaInvalidDataException()
        {
            // O ZipArchive recusa criar nomes inválidos; troca os bytes depois (cabeçalho local e diretório central).
            var zip = Zip(("c_d.srt", new byte[] { 5 }));
            for (var i = 0; i < zip.Length; i++) if (zip[i] == (byte)'_') zip[i] = (byte)'|';
            Assert.Throws<InvalidDataException>(() => Salvamento.ExtrairDoZip(zip));
        }

        [Fact]
        public void ExtrairDoZip_IgnoraArquivosQueNaoSaoLegenda()
        {
            var r = Salvamento.ExtrairDoZip(Zip(("leia.txt", new byte[] { 9 }), ("a.ASS", new byte[] { 7 })));
            Assert.Equal(".ass", r.Extensao);
            Assert.Equal(new byte[] { 7 }, r.Bytes);
        }

        [Fact]
        public void ExtrairDoZip_SemLegenda_DevolveNulo() =>
            Assert.Null(Salvamento.ExtrairDoZip(Zip(("leia.txt", new byte[] { 9 }))));

        [Fact]
        public void ExtrairDoZip_ZipInvalido_Lanca() =>
            Assert.Throws<InvalidDataException>(() => Salvamento.ExtrairDoZip(new byte[] { 0x50, 0x4B, 1, 2 }));

        [Fact]
        public void ExtrairDoZip_LegendaMaiorQue10MB_Lanca() =>
            Assert.Throws<InvalidDataException>(() => Salvamento.ExtrairDoZip(Zip(("a.srt", new byte[10 * 1024 * 1024 + 1]))));

        [Fact]
        public void ExtrairDoZip_LengthMentiroso_ParaNoLimite()
        {
            var zip = Zip(("a.srt", new byte[10 * 1024 * 1024 + 1]));
            // Tamanho descompactado declarado = 3: local header (offset 22) e diretório central (offset 24).
            var tres = BitConverter.GetBytes(3);
            Array.Copy(tres, 0, zip, 22, 4);
            var central = new byte[] { 0x50, 0x4B, 0x01, 0x02 };
            var i = IndiceDe(zip, central);
            Array.Copy(tres, 0, zip, i + 24, 4);
            Assert.Throws<InvalidDataException>(() => Salvamento.ExtrairDoZip(zip));
        }

        static int IndiceDe(byte[] dados, byte[] padrao)
        {
            for (var i = dados.Length - padrao.Length; i >= 0; i--)
            {
                var ok = true;
                for (var j = 0; j < padrao.Length && ok; j++) ok = dados[i + j] == padrao[j];
                if (ok) return i;
            }
            return -1;
        }

        [Fact]
        public void ParaUtf8_Windows1252_ConverteComBom()
        {
            var cp1252 = Encoding.GetEncoding(1252).GetBytes("Não, você é ótimo.");
            var r = Salvamento.ParaUtf8(cp1252);
            Assert.Equal(Bom, new[] { r[0], r[1], r[2] });
            Assert.Equal("Não, você é ótimo.", new UTF8Encoding(false).GetString(r, 3, r.Length - 3));
        }

        [Fact]
        public void ParaUtf8_JaUtf8_MantemIgual()
        {
            var utf8 = new UTF8Encoding(false).GetBytes("Não");
            Assert.Equal(utf8, Salvamento.ParaUtf8(utf8));
            var comBom = new byte[] { 0xEF, 0xBB, 0xBF, 0x41 };
            Assert.Equal(comBom, Salvamento.ParaUtf8(comBom));
        }

        [Fact]
        public void Gravar_MesmoNomeDoVideo_ComAcentosEspacosEColchetes()
        {
            var video = Path.Combine(_pasta, "Ação & Cia [2026].mkv");
            File.WriteAllText(video, "");
            var destino = Salvamento.Gravar(video, new ArquivoLegenda { Extensao = ".srt", Bytes = Encoding.ASCII.GetBytes("1") });
            Assert.Equal(Path.Combine(_pasta, "Ação & Cia [2026].srt"), destino);
            Assert.Equal("1", File.ReadAllText(destino));
        }

        [Fact]
        public void Gravar_SubstituiLegendaExistente()
        {
            var video = Path.Combine(_pasta, "Filme.mp4");
            File.WriteAllText(Path.Combine(_pasta, "Filme.srt"), "antiga");
            Salvamento.Gravar(video, new ArquivoLegenda { Extensao = ".srt", Bytes = Encoding.ASCII.GetBytes("nova") });
            Assert.Equal("nova", File.ReadAllText(Path.Combine(_pasta, "Filme.srt")));
        }

        [Fact]
        public void Gravar_NaoDeixaArquivoTemporario()
        {
            var video = Path.Combine(_pasta, "Filme.mkv");
            var legenda = new ArquivoLegenda { Extensao = ".srt", Bytes = Encoding.ASCII.GetBytes("a") };
            Salvamento.Gravar(video, legenda);
            Salvamento.Gravar(video, legenda);
            Assert.Equal(new[] { Path.Combine(_pasta, "Filme.srt") }, Directory.GetFiles(_pasta));
        }

        [Fact]
        public void Gravar_PreservaTemporarioPreexistente()
        {
            var video = Path.Combine(_pasta, "Filme.mkv");
            var auxiliar = Path.Combine(_pasta, "Filme.srt.tmp");
            File.WriteAllText(auxiliar, "arquivo de outro programa");

            var destino = Salvamento.Gravar(video,
                new ArquivoLegenda { Extensao = ".srt", Bytes = Encoding.ASCII.GetBytes("nova legenda") });

            Assert.Equal("nova legenda", File.ReadAllText(destino));
            Assert.Equal("arquivo de outro programa", File.ReadAllText(auxiliar));
            Assert.Equal(2, Directory.GetFiles(_pasta).Length);
        }

        [Fact]
        public void Gravar_FalhaDeSubstituicao_PreservaTemporarioPreexistente()
        {
            var video = Path.Combine(_pasta, "Filme.mkv");
            var destino = Path.Combine(_pasta, "Filme.srt");
            var auxiliar = destino + ".tmp";
            File.WriteAllText(destino, "legenda antiga");
            File.WriteAllText(auxiliar, "arquivo de outro programa");

            using (File.Open(destino, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.ThrowsAny<IOException>(() => Salvamento.Gravar(video,
                    new ArquivoLegenda { Extensao = ".srt", Bytes = Encoding.ASCII.GetBytes("nova legenda") }));

            Assert.Equal("legenda antiga", File.ReadAllText(destino));
            Assert.Equal("arquivo de outro programa", File.ReadAllText(auxiliar));
            Assert.Equal(2, Directory.GetFiles(_pasta).Length);
        }

        [Fact]
        public void Gravar_LegendaExistenteSomenteLeitura_Substitui()
        {
            var existente = Path.Combine(_pasta, "Filme.srt");
            File.WriteAllText(existente, "antiga");
            File.SetAttributes(existente, FileAttributes.ReadOnly);
            Salvamento.Gravar(Path.Combine(_pasta, "Filme.mp4"), new ArquivoLegenda { Extensao = ".srt", Bytes = Encoding.ASCII.GetBytes("nova") });
            Assert.Equal("nova", File.ReadAllText(existente));
        }

        [Fact]
        public void Gravar_PastaInexistente_Lanca() =>
            Assert.ThrowsAny<IOException>(() => Salvamento.Gravar(Path.Combine(_pasta, "nao-existe", "Filme.mkv"),
                new ArquivoLegenda { Extensao = ".srt", Bytes = new byte[] { 1 } }));
    }
}
