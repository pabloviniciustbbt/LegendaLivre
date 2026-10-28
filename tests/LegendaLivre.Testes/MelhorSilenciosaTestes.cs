using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace LegendaLivre.Testes
{
    public class MelhorSilenciosaTestes
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Melhor_EncerraSemAbrirJanelaMesmoSemLegendaOuComFalhaLeitura(bool criarVideo)
        {
            var pasta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados-testes", "MelhorSilenciosa-" + Guid.NewGuid());
            Directory.CreateDirectory(pasta);
            var video = Path.Combine(pasta, "Vídeo de teste.mkv");
            if (criarVideo) File.WriteAllBytes(video, new byte[0]);
            var abertas = 0;
            var retorno = -1;
            Exception erro = null;
            try
            {
                var tarefa = new Thread(() =>
                {
                    try
                    {
                        Application.EnableVisualStyles();
                        using (var vigia = new System.Windows.Forms.Timer { Interval = 10 })
                        {
                            // Fecha a janela antiga para que a regressão falhe sem bloquear a suíte.
                            vigia.Tick += (s, e) =>
                            {
                                var janelas = Application.OpenForms.Cast<Form>().Where(f => !f.InvokeRequired).ToArray();
                                abertas += janelas.Length;
                                foreach (var janela in janelas) janela.Close();
                            };
                            vigia.Start();
                            var programa = typeof(Instalador).Assembly.GetType("LegendaLivre.Programa");
                            var metodo = programa.GetMethod("Melhor", BindingFlags.Static | BindingFlags.NonPublic);
                            var comando = Comando.Interpretar(new[] { "melhor", "6", video });
                            retorno = (int)metodo.Invoke(null, new object[] { comando });
                            abertas += Application.OpenForms.Cast<Form>().Count(f => !f.InvokeRequired);
                        }
                    }
                    catch (Exception excecao) { erro = excecao; }
                }) { IsBackground = true };
                tarefa.SetApartmentState(ApartmentState.STA);
                tarefa.Start();
                Assert.True(tarefa.Join(10000), "O fluxo automático não encerrou.");
                Assert.Null(erro);
                Assert.Equal(0, retorno);
                Assert.Equal(0, abertas);
            }
            finally { Directory.Delete(pasta, true); }
        }
    }
}
