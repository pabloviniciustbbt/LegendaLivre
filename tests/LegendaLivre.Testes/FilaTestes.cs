using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LegendaLivre.Testes
{
    public class FilaTestes
    {
        static string NomeUnico() => "LegendaLivreTeste." + Guid.NewGuid().ToString("N");

        [Fact]
        public void PermissoesDoPipe_NegamRedeEPermitemSoOUsuarioAtual()
        {
            var regras = Fila.SoUsuarioAtual().GetAccessRules(true, false, typeof(System.Security.Principal.SecurityIdentifier));
            var negadas = 0;
            var permitidas = 0;
            foreach (System.IO.Pipes.PipeAccessRule regra in regras)
                if (regra.AccessControlType == System.Security.AccessControl.AccessControlType.Deny
                    && regra.IdentityReference.Value == "S-1-5-2") negadas++;
                else if (regra.AccessControlType == System.Security.AccessControl.AccessControlType.Allow) permitidas++;
            Assert.Equal(1, negadas);
            Assert.Equal(1, permitidas);
        }

        [Fact]
        public void Primeiro_ViraPrincipalComOArquivo()
        {
            using (var fila = Fila.Abrir(NomeUnico(), "a.mkv"))
            {
                Assert.True(fila.EhPrincipal);
                Assert.True(fila.TentarPegar(0, out var arquivo));
                Assert.Equal("a.mkv", arquivo);
                Assert.False(fila.TentarPegar(0, out _));
            }
        }

        [Fact]
        public void Segundo_EntregaParaOPrincipalEDevolveNulo()
        {
            var nome = NomeUnico();
            using (var principal = Fila.Abrir(nome, "a.mkv"))
            {
                // Outro thread simula outro processo (mutex é por thread).
                var segundo = Task.Run(() => Fila.Abrir(nome, "Ação & Cia [2026].mkv")).Result;
                Assert.Null(segundo);
                Assert.True(principal.TentarPegar(0, out _));
                Assert.True(principal.TentarPegar(3000, out var recebido));
                Assert.Equal("Ação & Cia [2026].mkv", recebido);
            }
        }

        [Fact]
        public void PipeIndisponivel_ProcessaSozinho()
        {
            var nome = NomeUnico();
            using (var mutexAlheio = new Mutex(true, @"Local\" + nome))
            {
                var fila = Task.Run(() => Fila.Abrir(nome, "b.mkv")).Result;
                Assert.NotNull(fila);
                Assert.False(fila.EhPrincipal);
                Assert.True(fila.TentarPegar(0, out var arquivo));
                Assert.Equal("b.mkv", arquivo);
                fila.Dispose();
                mutexAlheio.ReleaseMutex();
            }
        }

        [Fact]
        public void MutexInacessivel_ProcessaSozinho()
        {
            var nome = NomeUnico();
            // Um objeto de outro tipo com o mesmo nome faz o ctor do Mutex lançar WaitHandleCannotBeOpenedException.
            using (new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\" + nome))
            using (var fila = Fila.Abrir(nome, "e.mkv"))
            {
                Assert.NotNull(fila);
                Assert.False(fila.EhPrincipal);
                Assert.True(fila.TentarPegar(0, out var arquivo));
                Assert.Equal("e.mkv", arquivo);
            }
        }

        [Fact]
        public void DepoisDeFechar_NovoAbrirViraPrincipal()
        {
            var nome = NomeUnico();
            Fila.Abrir(nome, "a.mkv").Dispose();
            using (var outra = Task.Run(() => Fila.Abrir(nome, "c.mkv")).Result)
                Assert.True(outra.EhPrincipal);
        }

        [Fact]
        public void NomePara_IncluiUsuarioSessaoModoECodigo() =>
            Assert.Equal($"LegendaLivre.{Environment.UserName}.{System.Diagnostics.Process.GetCurrentProcess().SessionId}.melhor.6", Fila.NomePara("melhor", 6));

        [Fact]
        public void ClienteQueNaoEscreve_NaoTravaAFila()
        {
            var nome = NomeUnico();
            using (var principal = Fila.Abrir(nome, "a.mkv"))
            {
                principal.LimiteConexaoMs = 300;
                Assert.True(principal.TentarPegar(0, out _));
                using (var travado = new System.IO.Pipes.NamedPipeClientStream(".", nome, System.IO.Pipes.PipeDirection.Out))
                {
                    travado.Connect(2000);
                    Assert.Null(Task.Run(() => Fila.Abrir(nome, "b.mkv")).Result);
                    Assert.True(principal.TentarPegar(10000, out var recebido));
                    Assert.Equal("b.mkv", recebido);
                }
            }
        }

        [Fact]
        public void Encerrar_DevolvePendentes()
        {
            using (var fila = Fila.Abrir(NomeUnico(), "a.mkv"))
                Assert.Equal(new[] { "a.mkv" }, fila.Encerrar());
        }

        [Fact]
        public async Task Encerrar_DuranteNovaEsperaDoPipe_NaoFalhaAoCancelarHandleFechado()
        {
            await Task.Run(() =>
            {
                for (var rodada = 0; rodada < 100; rodada++)
                {
                    var nome = NomeUnico();
                    using (var principal = Fila.Abrir(nome, "a.mkv"))
                    {
                        Assert.Null(Task.Run(() => Fila.Abrir(nome, "b.mkv")).GetAwaiter().GetResult());
                        Assert.True(principal.TentarPegar(0, out _));
                        Assert.True(principal.TentarPegar(3000, out var recebido));
                        Assert.Equal("b.mkv", recebido);
                        principal.Encerrar();
                    }
                }
            });
        }

        [Fact]
        public void Encerrar_DepoisDeEncerrar_NovoClienteProcessaSozinho()
        {
            var nome = NomeUnico();
            using (var principal = Fila.Abrir(nome, "a.mkv"))
            {
                principal.Encerrar();
                using (var outra = Task.Run(() => Fila.Abrir(nome, "d.mkv")).Result)
                {
                    Assert.NotNull(outra);
                    Assert.False(outra.EhPrincipal);
                    Assert.True(outra.TentarPegar(0, out var arquivo));
                    Assert.Equal("d.mkv", arquivo);
                }
            }
        }
    }
}
