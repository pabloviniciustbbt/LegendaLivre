using Xunit;

namespace LegendaLivre.Testes
{
    public class ComandoTestes
    {
        [Fact]
        public void SemArgumentos_PerguntaInstalar() =>
            Assert.Equal(Acao.PerguntarInstalar, Comando.Interpretar(new string[0]).Acao);

        [Fact]
        public void Instalar() => Assert.Equal(Acao.Instalar, Comando.Interpretar(new[] { "--instalar" }).Acao);

        [Fact]
        public void Desinstalar() => Assert.Equal(Acao.Desinstalar, Comando.Interpretar(new[] { "--desinstalar" }).Acao);

        [Fact]
        public void RemoverPasta()
        {
            var c = Comando.Interpretar(new[] { "--remover-pasta", @"C:\Users\x\AppData\Local\Programs\LegendaLivre", "1234" });
            Assert.Equal(Acao.RemoverPasta, c.Acao);
            Assert.Equal(@"C:\Users\x\AppData\Local\Programs\LegendaLivre", c.Pasta);
            Assert.Equal(1234, c.Pid);
        }

        [Theory]
        [InlineData("melhor", Acao.Melhor)]
        [InlineData("top", Acao.Top)]
        public void Modos(string modo, Acao acao)
        {
            var c = Comando.Interpretar(new[] { modo, "36", @"D:\Vídeos\Ação & Cia [2026].mkv" });
            Assert.Equal(acao, c.Acao);
            Assert.Same(Idioma.PortuguesPortugal, c.Idioma);
            Assert.Equal(@"D:\Vídeos\Ação & Cia [2026].mkv", c.Arquivo);
        }

        [Theory]
        [InlineData("melhor")]
        [InlineData("melhor", "6")]
        [InlineData("melhor", "99", "a.mkv")]
        [InlineData("melhor", "x", "a.mkv")]
        [InlineData("outro", "6", "a.mkv")]
        [InlineData("--remover-pasta")]
        [InlineData("--remover-pasta", "C:\\x", "abc")]
        public void Invalidos_DevolvemNulo(params string[] args) => Assert.Null(Comando.Interpretar(args));

        [Fact]
        public void LinhaDoMenu_PoeAspasNoExeENoArquivo() =>
            Assert.Equal("\"C:\\P\\LegendaLivre.exe\" melhor 6 \"%1\"", Comando.LinhaDoMenu(@"C:\P\LegendaLivre.exe", "melhor", 6));
    }
}
