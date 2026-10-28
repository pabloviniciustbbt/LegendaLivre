using System;
using System.IO;
using Xunit;

namespace LegendaLivre.Testes
{
    public class ImpressaoDigitalTestes : IDisposable
    {
        readonly string _pasta = Path.Combine(Path.GetTempPath(), "LegendaLivreHash-" + Guid.NewGuid());

        public ImpressaoDigitalTestes() => Directory.CreateDirectory(_pasta);
        public void Dispose() => Directory.Delete(_pasta, true);

        string Criar(byte[] bytes)
        {
            var caminho = Path.Combine(_pasta, Guid.NewGuid() + ".bin");
            File.WriteAllBytes(caminho, bytes);
            return caminho;
        }

        // Implementação de referência independente: lê o arquivo inteiro e soma com BigInteger-free ulong.
        static string Referencia(byte[] dados)
        {
            ulong soma = (ulong)dados.Length;
            int bloco = 65536;
            int tamCabeca = Math.Min(bloco, dados.Length);
            int inicioCauda = Math.Max(0, dados.Length - bloco);
            unchecked
            {
                for (int i = 0; i + 8 <= tamCabeca; i += 8) soma += BitConverter.ToUInt64(dados, i);
                if (dados.Length >= bloco)
                    for (int i = inicioCauda; i + 8 <= dados.Length; i += 8) soma += BitConverter.ToUInt64(dados, i);
            }
            return soma.ToString("x16");
        }

        [Fact]
        public void VetorFeitoAMao_16Bytes()
        {
            var dados = new byte[16];
            dados[0] = 1;
            dados[8] = 2;
            // tamanho 16 + cabeça (1 + 2); a cauda anterior ao início do arquivo não é somada.
            Assert.Equal("0000000000000013", ImpressaoDigital.Calcular(Criar(dados)));
        }

        [Theory]
        [InlineData(1003)]
        [InlineData(65535)]
        [InlineData(65536)]
        [InlineData(65537)]
        [InlineData(131072)]
        [InlineData(300001)]
        public void ConfereComReferencia(int tamanho)
        {
            var dados = new byte[tamanho];
            new Random(tamanho).NextBytes(dados);
            Assert.Equal(Referencia(dados), ImpressaoDigital.Calcular(Criar(dados)));
        }

        [Fact]
        public void ArquivoVazio_DevolveZeros() =>
            Assert.Equal("0000000000000000", ImpressaoDigital.Calcular(Criar(new byte[0])));

        [Fact]
        public void ProtocoloDaFonte_ArquivoMenorQue64KbSomaSomenteCabeca()
        {
            var dados = new byte[16];
            dados[0] = 1;
            dados[8] = 2;
            Assert.Equal("0000000000000013", ImpressaoDigital.Calcular(Criar(dados)));
        }

        [Fact]
        public void ArquivoInexistente_Lanca() =>
            Assert.Throws<FileNotFoundException>(() => ImpressaoDigital.Calcular(Path.Combine(_pasta, "nao-existe.mkv")));

        [Fact]
        public void ArquivoAbertoPorOutroPrograma_AindaCalcula()
        {
            var caminho = Criar(new byte[2048]);
            using (new FileStream(caminho, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                Assert.Equal(16, ImpressaoDigital.Calcular(caminho).Length);
        }
    }
}
