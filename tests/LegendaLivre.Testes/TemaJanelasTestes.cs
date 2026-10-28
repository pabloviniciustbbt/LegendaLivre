using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace LegendaLivre.Testes
{
    public class TemaJanelasTestes
    {
        static void EmSta(ThreadStart acao)
        {
            Exception erro = null;
            var thread = new Thread(() => { try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); acao(); } catch (Exception e) { erro = e; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (erro != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(erro).Throw();
        }

        [Fact]
        public void TodasAsJanelas_TrocamTemaSemPerderAtalhos() => EmSta(() =>
        {
            using (var aviso = new JanelaAviso("Aviso", "Texto do aviso", "Entendi"))
            using (var top = new JanelaTop10("video.mkv", new List<Legenda> { new Legenda { Nome = "Legenda", Id = "1", Data = "01-01-2026" } }, null))
                foreach (var janela in new JanelaTematica[] { aviso, top })
                {
                    var aceitar = janela.AcceptButton;
                    var cancelar = janela.CancelButton;
                    foreach (var modo in new[] { ModoTema.Claro, ModoTema.Escuro, ModoTema.AltoContraste })
                    {
                        var cores = CoresTema.Para(modo);
                        janela.AplicarTema(cores);
                        Assert.Equal(cores.Fundo, janela.BackColor);
                        Assert.Equal(cores.Texto, janela.ForeColor);
                        Assert.Same(aceitar, janela.AcceptButton);
                        Assert.Same(cancelar, janela.CancelButton);
                    }
                }
        });

        [Fact]
        public void Top10_TrocaTemaPreservandoSelecaoEEstadoDesabilitado() => EmSta(() =>
        {
            using (var janela = new JanelaTop10("video.mkv", new List<Legenda>
            {
                new Legenda { Nome = "Primeira", Id = "1" }, new Legenda { Nome = "Segunda", Id = "2" }
            }, null))
            {
                var lista = (ListView)janela.Controls.Find("lista", true)[0];
                lista.Items[0].Selected = false;
                lista.Items[1].Selected = true;
                lista.Items[1].Checked = true;
                var botao = (Button)janela.AcceptButton;
                botao.Enabled = false;
                janela.AplicarTema(CoresTema.Para(ModoTema.Escuro));
                Assert.Equal(CoresTema.Para(ModoTema.Escuro).Fundo, lista.BackColor);
                Assert.Equal(2, lista.CheckedItems.Count);
                Assert.True(lista.Items[1].Selected);
                Assert.False(botao.Enabled);
                Assert.False(string.IsNullOrWhiteSpace(botao.AccessibleName));
                janela.AplicarTema(CoresTema.Para(ModoTema.AltoContraste));
                Assert.False(lista.OwnerDraw);
                Assert.Equal(SystemColors.Window, lista.BackColor);
                Assert.Equal(FlatStyle.System, botao.FlatStyle);
            }
        });

        [Fact]
        public void NotificacaoDoWindows_ReaplicaTemaNaMesmaJanela() => EmSta(() =>
        {
            var cores = CoresTema.Para(ModoTema.Claro);
            using (var janela = new JanelaDeTeste(() => cores))
            {
                var handle = janela.Handle;
                Assert.Equal(Color.FromArgb(243, 243, 243), janela.BackColor);
                cores = CoresTema.Para(ModoTema.Escuro);
                janela.Notificar();
                Assert.Equal(Color.FromArgb(32, 32, 32), janela.BackColor);
                Assert.Equal(handle, janela.Handle);
            }
        });

        [Fact]
        public void Confirmacao_ConservaSimNaoEAtalhoEsc() => EmSta(() =>
        {
            using (var janela = new JanelaAviso("Instalar", "Deseja instalar?", MessageBoxButtons.YesNo))
            {
                Assert.Equal(DialogResult.Yes, ((Button)janela.AcceptButton).DialogResult);
                Assert.Equal(DialogResult.No, ((Button)janela.CancelButton).DialogResult);
                Assert.Equal("Sim", ((Button)janela.AcceptButton).Text);
                Assert.Equal("Não", ((Button)janela.CancelButton).Text);
            }
        });

        sealed class JanelaDeTeste : JanelaTematica
        {
            public JanelaDeTeste(Func<CoresTema> obter) : base(obter) { }
            public void Notificar()
            {
                var mensagem = Message.Create(Handle, 0x001A, IntPtr.Zero, IntPtr.Zero);
                WndProc(ref mensagem);
            }
        }
    }
}
