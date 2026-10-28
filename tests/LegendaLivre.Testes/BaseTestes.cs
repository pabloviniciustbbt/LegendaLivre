using System;
using System.IO;
using Xunit;

namespace LegendaLivre.Testes
{
    public class BaseTestes
    {
        [Theory]
        [InlineData(6, "Português (Brasil)")]
        [InlineData(36, "Português (Portugal)")]
        [InlineData(14, "Inglês")]
        public void Idioma_PorCodigo_DevolveNome(int codigo, string nome) =>
            Assert.Equal(nome, Idioma.PorCodigo(codigo).Nome);

        [Fact]
        public void Idioma_CodigoDesconhecido_DevolveNulo() => Assert.Null(Idioma.PorCodigo(99));

        [Fact]
        public void Log_GravaEFazRotacaoAcimaDe1MB()
        {
            var pasta = Path.Combine(Path.GetTempPath(), "LegendaLivreTeste-" + Guid.NewGuid());
            var caminho = Path.Combine(pasta, "log.txt");
            try
            {
                Directory.CreateDirectory(pasta);
                File.WriteAllText(caminho, new string('x', 1024 * 1024 + 10));
                Log.EscreverEm(caminho, "INFO", "depois da rotação");
                Assert.True(File.Exists(caminho + ".1"));
                Assert.Contains("depois da rotação", File.ReadAllText(caminho));
                Assert.True(new FileInfo(caminho).Length < 1024);
            }
            finally { Directory.Delete(pasta, true); }
        }
    }
}
