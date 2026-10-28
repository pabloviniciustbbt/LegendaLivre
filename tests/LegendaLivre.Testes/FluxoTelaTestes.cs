using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace LegendaLivre.Testes
{
    public class FluxoTelaTestes
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void Escolha_BaixaMarcadasEMostraAcoesDoResultado(int quantidade) => EmSta(() =>
        {
            var pasta = CriarPasta();
            try
            {
                var video = Path.Combine(pasta, "Filme.1999.mkv");
                var existente = Path.Combine(pasta, "Filme.1999.legenda-2.srt");
                File.WriteAllText(existente, "preservar");
                var chamadas = 0;
                var zip = CriarZip();
                var servico = new ServicoLegendas(new ClienteLegendas(url =>
                {
                    Interlocked.Increment(ref chamadas);
                    return new Resposta { Corpo = zip };
                }), arquivo => "123");
                using (var janela = Abrir(video, servico))
                {
                    var lista = (ListView)janela.Controls.Find("lista", true)[0];
                    Assert.True(lista.Items[0].Checked);
                    if (quantidade == 2) lista.Items[1].Checked = true;
                    Assert.Equal(quantidade, lista.CheckedItems.Count);
                    var baixar = (Button)janela.AcceptButton;
                    Assert.True(baixar.Enabled);
                    baixar.PerformClick();
                    Assert.False(baixar.Visible);
                    Aguardar(() => ((Button)janela.CancelButton).Text == "Fechar"
                        && ((Button)janela.AcceptButton).Text == (quantidade == 1 ? "Dar o play" : "Fechar"));
                    Assert.Equal(quantidade, chamadas);
                    Assert.Equal("legenda", File.ReadAllText(Path.ChangeExtension(video, ".srt")));
                    Assert.Equal("preservar", File.ReadAllText(existente));
                    Assert.Equal(quantidade == 2, File.Exists(Path.Combine(pasta, "Filme.1999.legenda-3.srt")));
                    Assert.Equal(quantidade == 1 ? "Dar o play" : "Fechar", ((Button)janela.AcceptButton).Text);
                    Assert.Equal(new Size(860, 560), janela.ClientSize);
                    ((Button)janela.CancelButton).PerformClick();
                    Assert.True(janela.IsDisposed);
                }
            }
            finally { Directory.Delete(pasta, true); }
        });

        [Fact]
        public void Download_CancelarFechaImediatamenteEImpedeGravacao() => EmSta(() =>
        {
            var pasta = CriarPasta();
            using (var iniciou = new ManualResetEventSlim())
            using (var liberar = new ManualResetEventSlim())
            using (var retornou = new ManualResetEventSlim())
            {
                try
                {
                    var video = Path.Combine(pasta, "Filme.mkv");
                    var zip = CriarZip();
                    var servico = new ServicoLegendas(new ClienteLegendas(url =>
                    {
                        iniciou.Set();
                        if (!liberar.Wait(5000)) throw new TimeoutException("Download de teste não liberado");
                        retornou.Set();
                        return new Resposta { Corpo = zip };
                    }), arquivo => "123");
                    using (var janela = Abrir(video, servico))
                    {
                        ((Button)janela.AcceptButton).PerformClick();
                        Aguardar(() => iniciou.IsSet);
                        Assert.False(((Button)janela.AcceptButton).Visible);
                        Assert.Equal("Cancelar", ((Button)janela.CancelButton).Text);
                        ((Button)janela.CancelButton).PerformClick();
                        Assert.True(janela.IsDisposed);
                        liberar.Set();
                        Aguardar(() => retornou.IsSet);
                        // Processa também a continuação da tarefa, após o fechamento.
                        var tempo = Stopwatch.StartNew();
                        while (tempo.ElapsedMilliseconds < 200) { Application.DoEvents(); Thread.Sleep(5); }
                        Assert.False(File.Exists(Path.ChangeExtension(video, ".srt")));
                    }
                }
                finally { liberar.Set(); Directory.Delete(pasta, true); }
            }
        });

        static JanelaTop10 Abrir(string video, ServicoLegendas servico)
        {
            var janela = new JanelaTop10(video, new List<Legenda>
            {
                new Legenda { Nome = "Primeira", Id = "1", Data = "01-01-2020" },
                new Legenda { Nome = "Segunda", Id = "2", Data = "01-01-2020" }
            }, servico);
            janela.ShowInTaskbar = false;
            janela.StartPosition = FormStartPosition.Manual;
            janela.Location = new Point(-10000, -10000);
            janela.Show();
            Application.DoEvents();
            Assert.IsType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
            return janela;
        }

        static string CriarPasta()
        {
            var pasta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados-testes", "LegendaLivre.Fluxo." + Guid.NewGuid());
            Directory.CreateDirectory(pasta);
            return pasta;
        }

        static byte[] CriarZip()
        {
            using (var memoria = new MemoryStream())
            {
                using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
                using (var escritor = new StreamWriter(zip.CreateEntry("legenda.srt").Open())) escritor.Write("legenda");
                return memoria.ToArray();
            }
        }

        static void Aguardar(Func<bool> terminou)
        {
            var tempo = Stopwatch.StartNew();
            while (!terminou() && tempo.ElapsedMilliseconds < 5000) { Application.DoEvents(); Thread.Sleep(5); }
            Assert.True(terminou(), "Fluxo da janela não terminou dentro do prazo");
        }

        static void EmSta(ThreadStart acao)
        {
            Exception erro = null;
            var tarefa = new Thread(() =>
            {
                try
                {
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    Application.EnableVisualStyles();
                    using (var hospedeira = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) })
                    {
                        hospedeira.Shown += (s, e) => hospedeira.BeginInvoke(new Action(() =>
                        {
                            try { acao(); }
                            catch (Exception excecao) { erro = excecao; }
                            finally { hospedeira.Close(); }
                        }));
                        Application.Run(hospedeira);
                    }
                }
                catch (Exception excecao) { erro = excecao; }
            });
            tarefa.SetApartmentState(ApartmentState.STA);
            tarefa.Start();
            tarefa.Join();
            if (erro != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(erro).Throw();
        }
    }
}
