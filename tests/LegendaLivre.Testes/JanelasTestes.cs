using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace LegendaLivre.Testes
{
    // Garante que as janelas montam sem erro e que teclado e foco estão configurados.
    public class JanelasTestes
    {
        static void EmSta(ThreadStart acao)
        {
            System.Exception erro = null;
            var t = new Thread(() => { try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); acao(); } catch (System.Exception e) { erro = e; } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (erro != null) throw erro;
        }

        [Fact]
        public void Top10_AbreComPrimeiroItemSelecionadoEAtalhos() => EmSta(() =>
        {
            var legendas = new List<Legenda> { new Legenda { Nome = "A", Id = "1", Data = "01-01-2020" }, new Legenda { Nome = "B", Id = "2", Data = "02-02-2020" } };
            using (var j = new JanelaTop10(@"C:\x\Filme.mkv", legendas, null))
            {
                j.CreateControl();
                var lista = (ListView)j.Controls.Find("lista", true)[0];
                Assert.Equal(2, lista.Items.Count);
                Assert.True(lista.Items[0].Selected);
                Assert.NotNull(j.AcceptButton);
                Assert.NotNull(j.CancelButton);
            }
        });

        [Fact]
        public void Top10_ListaVazia_NaoQuebra() => EmSta(() =>
        {
            using (var j = new JanelaTop10(@"C:\x\Filme.mkv", new List<Legenda>(), null))
            {
                j.CreateControl();
                Assert.Empty(((ListView)j.Controls.Find("lista", true)[0]).Items);
            }
        });

        [Fact]
        public void Top10_MarcacoesControlamQuantidadeEAcao() => EmSta(() =>
        {
            using (var janela = new JanelaTop10("The.Matrix.1999.mkv", new List<Legenda>
            {
                new Legenda { Nome = "Primeira", Id = "1" }, new Legenda { Nome = "Segunda", Id = "2" }
            }, null))
            {
                janela.ShowInTaskbar = false;
                janela.StartPosition = FormStartPosition.Manual;
                janela.Location = new System.Drawing.Point(-10000, -10000);
                janela.Show();
                var lista = (ListView)janela.Controls.Find("lista", true)[0];
                Assert.True(lista.Items[0].Checked);
                Assert.Equal("Baixar Legenda", ((Button)janela.AcceptButton).Text);
                lista.Items[1].Checked = true;
                Assert.Equal("Baixar Legendas", ((Button)janela.AcceptButton).Text);
                lista.Items[0].Checked = lista.Items[1].Checked = false;
                Assert.False(((Button)janela.AcceptButton).Enabled);
                janela.Close();
            }
        });

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        public void Top10_ResultadoUsaQuantidadeRealmenteSalva(int salvas, bool permitePlay) => EmSta(() =>
        {
            using (var janela = new JanelaTop10("Filme.mkv", new List<Legenda>(), null))
            {
                janela.ShowInTaskbar = false;
                janela.StartPosition = FormStartPosition.Manual;
                janela.Location = new System.Drawing.Point(-10000, -10000);
                janela.Show();
                var resultados = new List<ResultadoDownload> { new ResultadoDownload { Status = Status.FalhaDownload } };
                for (var i = 0; i < salvas; i++) resultados.Add(new ResultadoDownload { Status = Status.Salva });
                janela.MostrarResultado(resultados);
                Assert.Equal("Fechar", ((Button)janela.CancelButton).Text);
                Assert.True(((Button)janela.CancelButton).Visible);
                Assert.Equal(permitePlay ? "Dar o play" : "Fechar", ((Button)janela.AcceptButton).Text);
                Assert.Equal(new System.Drawing.Size(860, 560), janela.ClientSize);
                janela.PerformLayout();
                if (!permitePlay)
                {
                    var fechar = (Button)janela.CancelButton;
                    var centro = janela.PointToClient(fechar.PointToScreen(new System.Drawing.Point(fechar.Width / 2, fechar.Height / 2)));
                    Assert.InRange(System.Math.Abs(centro.X - janela.ClientSize.Width / 2), 0, 2);
                }
                ((Button)janela.CancelButton).PerformClick();
                Assert.True(janela.IsDisposed);
            }
        });

        [Fact]
        public void Aviso_UsaTextoDoBotao() => EmSta(() =>
        {
            using (var j = new JanelaAviso(Mensagens.InstaladoTitulo, Mensagens.InstaladoTexto, Mensagens.Fechar))
            {
                j.CreateControl();
                Assert.Equal(Mensagens.Fechar, ((Button)j.AcceptButton).Text);
            }
        });

        [Fact]
        public void Top10_CancelarFechaJanelaNaoModal() => EmSta(() =>
        {
            var j = new JanelaTop10(@"C:\x\Filme.mkv", new List<Legenda>(), null);
            j.Show();
            ((Button)j.CancelButton).PerformClick();
            Assert.True(j.IsDisposed || !j.Visible);
        });

        [Fact]
        public void Janelas_UsamEscalaDpiComDimensaoBase() => EmSta(() =>
        {
            using (var a = new JanelaAviso("t", "x", "ok"))
            using (var c = new JanelaTop10(@"C:\x\Filme.mkv", new List<Legenda>(), null))
                foreach (var f in new Form[] { a, c })
                {
                    Assert.Equal(AutoScaleMode.Dpi, f.AutoScaleMode);
                    Assert.Equal(new System.Drawing.SizeF(96F, 96F), f.AutoScaleDimensions);
                }
        });
    }
}
