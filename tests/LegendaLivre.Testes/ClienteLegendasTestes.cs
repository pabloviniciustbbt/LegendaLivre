using System.Collections.Generic;
using System.Text;
using Xunit;

namespace LegendaLivre.Testes
{
    public class ClienteLegendasTestes
    {
        static Resposta Texto(string s) => new Resposta { Corpo = Encoding.UTF8.GetBytes(s), TipoConteudo = "text/xml;charset=UTF-8" };

        [Fact]
        public void InterpretarLista_Vazia() => Assert.Empty(ClienteLegendas.InterpretarLista(""));

        [Fact]
        public void InterpretarLista_UmRegistro()
        {
            var lista = ClienteLegendas.InterpretarLista(DadosFonte.Lista("Filme-2026-1080p-pob|71499183?17-02-2026>"));
            var l = Assert.Single(lista);
            Assert.Equal("Filme-2026-1080p-pob", l.Nome);
            Assert.Equal("71499183", l.Id);
            Assert.Equal("17-02-2026", l.Data);
        }

        [Fact]
        public void InterpretarLista_VariosMantendoOrdem()
        {
            var lista = ClienteLegendas.InterpretarLista(DadosFonte.Lista("A|1?01-01-2020>B|2?02-02-2021>C|3?03-03-2022>"));
            Assert.Equal(new[] { "A", "B", "C" }, new[] { lista[0].Nome, lista[1].Nome, lista[2].Nome });
        }

        [Fact]
        public void InterpretarLista_IgnoraRegistrosInvalidos()
        {
            var lista = ClienteLegendas.InterpretarLista(DadosFonte.Lista("sem-separador>X|abc?01-01-2020>Y|5?2020-01-01>Ok|7?07-07-2027>\r\n"));
            Assert.Equal("Ok", Assert.Single(lista).Nome);
        }

        [Fact]
        public void InterpretarLista_NomeComBarraVertical_UsaUltimaBarra()
        {
            var l = Assert.Single(ClienteLegendas.InterpretarLista(DadosFonte.Lista("Nome|com|barra|9?09-09-2019>")));
            Assert.Equal("Nome|com|barra", l.Nome);
            Assert.Equal("9", l.Id);
        }

        [Fact]
        public void Listar_MontaUrlCorreta()
        {
            string urlChamada = null;
            var cliente = new ClienteLegendas(url => { urlChamada = url; return Texto(DadosFonte.Lista("A|1?01-01-2020>")); });
            cliente.Listar("a4de2549514922e2", 36);
            Assert.Equal(ClienteLegendas.Base + "get_subtitles_by_hash.php?version=383&response=xml&cod_language=36&hash=YTRkZTI1NDk1MTQ5MjJlMg%3D%3D", urlChamada);
        }

        [Fact]
        public void BaixarZip_MontaUrlEDevolveCorpo()
        {
            string urlChamada = null;
            var zip = new byte[] { 0x50, 0x4B, 3, 4, 9, 9 };
            var cliente = new ClienteLegendas(url => { urlChamada = url; return new Resposta { Corpo = zip, TipoConteudo = "application/octet-stream" }; });
            var resultado = cliente.BaixarZip(new Legenda { Nome = "A", Id = "71499183", Data = "17-02-2026" });
            Assert.Equal(ClienteLegendas.Base + "get_subtitle_files.php?version=383&response=single&cod_bsplayer=NzE0OTkxODM%3D&post_date=MTctMDItMjAyNg%3D%3D", urlChamada);
            Assert.Equal(zip, resultado);
        }

        [Theory]
        [InlineData("")]
        [InlineData("<html>erro</html>")]
        public void BaixarZip_RespostaQueNaoEZip_Lanca(string corpo)
        {
            var cliente = new ClienteLegendas(url => Texto(corpo));
            Assert.Throws<FalhaFonteException>(() => cliente.BaixarZip(new Legenda { Nome = "A", Id = "1", Data = "01-01-2020" }));
        }

        [Fact]
        public void Listar_RespostaHtml_LancaFalhaFonte()
        {
            var cliente = new ClienteLegendas(url => Texto("<HTML><body>erro</body></HTML>"));
            Assert.Throws<FalhaFonteException>(() => cliente.Listar("0123456789abcdef", 6));
        }

        [Fact]
        public void InterpretarLista_NomeComAcento()
        {
            var cliente = new ClienteLegendas(url => Texto(DadosFonte.Lista("Ação|1?01-01-2020>")));
            Assert.Equal("Ação", Assert.Single(cliente.Listar("0123456789abcdef", 6)).Nome);
        }

        [Fact]
        public void LerComLimite_AbaixoDoLimite_DevolveBytes()
        {
            var dados = new byte[] { 1, 2, 3, 4, 5 };
            Assert.Equal(dados, ClienteLegendas.LerComLimite(new System.IO.MemoryStream(dados), 10));
        }

        [Fact]
        public void LerComLimite_AcimaDoLimite_Lanca()
        {
            Assert.Throws<FalhaFonteException>(() => ClienteLegendas.LerComLimite(new System.IO.MemoryStream(new byte[11]), 10));
        }

        [Fact]
        public void Listar_ErroDeRede_PropagaFalhaFonte()
        {
            var cliente = new ClienteLegendas(url => throw new FalhaFonteException("sem rede"));
            Assert.Throws<FalhaFonteException>(() => cliente.Listar("0123456789abcdef", 6));
        }
    }
}
