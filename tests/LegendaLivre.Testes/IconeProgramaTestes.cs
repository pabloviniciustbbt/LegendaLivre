using System.Drawing;
using System.IO;
using System.Linq;
using Xunit;

namespace LegendaLivre.Testes
{
    public class IconeProgramaTestes
    {
        [Fact]
        public void Recurso_ContemIconeMultirresolucao()
        {
            using (var recurso = typeof(IconePrograma).Assembly.GetManifestResourceStream("LegendaLivre.Assets.LegendaLivre.ico"))
            {
                Assert.NotNull(recurso);
                using (var leitor = new BinaryReader(recurso))
                {
                    Assert.Equal(0, leitor.ReadUInt16());
                    Assert.Equal(1, leitor.ReadUInt16());
                    var quantidade = leitor.ReadUInt16();
                    Assert.Equal(7, quantidade);
                    var tamanhos = new int[quantidade];
                    for (var i = 0; i < quantidade; i++)
                    {
                        var largura = leitor.ReadByte();
                        var altura = leitor.ReadByte();
                        Assert.Equal(largura, altura);
                        tamanhos[i] = largura == 0 ? 256 : largura;
                        leitor.ReadUInt16();
                        Assert.Equal(1, leitor.ReadUInt16());
                        Assert.Equal(32, leitor.ReadUInt16());
                        var tamanho = leitor.ReadUInt32();
                        var posicao = leitor.ReadUInt32();
                        Assert.True(tamanho > 0);
                        Assert.True(posicao >= 6 + 16 * quantidade);
                        Assert.True(posicao + tamanho <= recurso.Length);
                    }
                    Assert.Equal(new[] { 16, 24, 32, 48, 64, 128, 256 }, tamanhos.OrderBy(t => t));
                }
            }
        }

        [Theory]
        [InlineData(16)]
        [InlineData(24)]
        [InlineData(32)]
        [InlineData(48)]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(256)]
        public void Recurso_PermiteDesenharTodasAsDimensoes(int tamanho)
        {
            using (var recurso = typeof(IconePrograma).Assembly.GetManifestResourceStream("LegendaLivre.Assets.LegendaLivre.ico"))
            using (var leitor = new BinaryReader(recurso))
            {
                recurso.Position = 4;
                var quantidade = leitor.ReadUInt16();
                for (var i = 0; i < quantidade; i++)
                {
                    recurso.Position = 6 + 16 * i;
                    var largura = leitor.ReadByte();
                    if ((largura == 0 ? 256 : largura) != tamanho) continue;
                    recurso.Position += 7;
                    var bytes = leitor.ReadUInt32();
                    var posicao = leitor.ReadUInt32();
                    recurso.Position = posicao;
                    using (var quadro = new MemoryStream(leitor.ReadBytes((int)bytes)))
                    using (var imagem = new Bitmap(quadro))
                    {
                        Assert.Equal(tamanho, imagem.Width);
                        Assert.Equal(tamanho, imagem.Height);
                        Assert.Equal(0, imagem.GetPixel(0, 0).A);
                        Assert.True(imagem.GetPixel(tamanho / 2, tamanho / 2).A > 0);
                    }
                    return;
                }
                Assert.Fail("O ícone não contém a dimensão solicitada.");
            }
        }

        [Fact]
        public void Criar_ProduzInstanciasIndependentesDoRecursoEDoDescarte()
        {
            using (var primeiro = IconePrograma.Criar())
            using (var segundo = IconePrograma.Criar())
            {
                Assert.NotSame(primeiro, segundo);
                Assert.NotEqual(primeiro.Handle, segundo.Handle);
                primeiro.Dispose();
                using (var imagem = segundo.ToBitmap())
                    Assert.True(imagem.Width > 0);
            }
            using (var terceiro = IconePrograma.Criar())
            using (var imagem = terceiro.ToBitmap())
                Assert.True(imagem.Width > 0);
        }
    }
}
