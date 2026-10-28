using System;
using System.Drawing;
using System.Windows.Forms;
using Xunit;

namespace LegendaLivre.Testes
{
    public class TemaSistemaTestes
    {
        [Theory]
        [InlineData(false, 0, ModoTema.Escuro)]
        [InlineData(false, 1, ModoTema.Claro)]
        [InlineData(false, null, ModoTema.Claro)]
        [InlineData(true, 0, ModoTema.AltoContraste)]
        [InlineData(true, 1, ModoTema.AltoContraste)]
        public void Preferencia_EscolheTemaComPrioridadeParaAltoContraste(bool contraste, object valor, ModoTema esperado) =>
            Assert.Equal(esperado, TemaSistema.EscolherModo(contraste, valor));

        [Theory]
        [InlineData(ModoTema.Claro)]
        [InlineData(ModoTema.Escuro)]
        public void Paleta_MantemTextoEAcoesLegiveis(ModoTema modo)
        {
            var cores = CoresTema.Para(modo);
            Assert.True(Contraste(cores.Texto, cores.Fundo) >= 4.5);
            Assert.True(Contraste(cores.Texto, cores.Superficie) >= 4.5);
            Assert.True(Contraste(cores.TextoSecundario, cores.Fundo) >= 4.5);
            Assert.True(Contraste(cores.TextoSecundario, cores.Superficie) >= 4.5);
            Assert.True(Contraste(cores.TextoDestaque, cores.Destaque) >= 4.5);
            Assert.True(Contraste(cores.TextoSelecao, cores.Selecao) >= 4.5);
        }

        [Fact]
        public void AltoContraste_UsaCoresEscolhidasNoWindows()
        {
            var cores = CoresTema.Para(ModoTema.AltoContraste);
            Assert.Equal(SystemColors.Window, cores.Fundo);
            Assert.Equal(SystemColors.WindowText, cores.Texto);
            Assert.Equal(SystemColors.Highlight, cores.Destaque);
            Assert.Equal(SystemColors.HighlightText, cores.TextoDestaque);
            Assert.Equal(SystemColors.Highlight, cores.Selecao);
        }

        static double Contraste(Color a, Color b)
        {
            double x = Luminancia(a), y = Luminancia(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }

        static double Luminancia(Color cor) =>
            0.2126 * Linear(cor.R) + 0.7152 * Linear(cor.G) + 0.0722 * Linear(cor.B);

        static double Linear(byte valor)
        {
            var normal = valor / 255.0;
            return normal <= 0.04045 ? normal / 12.92 : Math.Pow((normal + 0.055) / 1.055, 2.4);
        }
    }
}
