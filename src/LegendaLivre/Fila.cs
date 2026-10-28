using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LegendaLivre
{
    // O Explorer abre um processo por vídeo. O primeiro vira principal; os outros entregam o caminho pelo pipe.
    public sealed class Fila : IDisposable
    {
        const int EsperaConexaoMs = 2000;
        internal int LimiteConexaoMs = 5000;
        const int PausaErroMs = 200;
        const int LimiteMensagemBytes = 32 * 1024;
        readonly BlockingCollection<string> _itens = new BlockingCollection<string>();
        readonly CancellationTokenSource _parar = new CancellationTokenSource();
        readonly Mutex _mutex;
        readonly string _nome;
        Task _servidor;
        bool _encerrada;

        public bool EhPrincipal => _mutex != null;

        Fila(Mutex mutex, string nome, string primeiroArquivo)
        {
            _mutex = mutex;
            _nome = nome;
            _itens.Add(primeiroArquivo);
        }

        public static string NomePara(string modo, int codigo)
        {
            using (var processo = Process.GetCurrentProcess())
                return $"LegendaLivre.{Environment.UserName}.{processo.SessionId}.{modo}.{codigo}";
        }

        public static Fila Abrir(string nome, string primeiroArquivo)
        {
            Mutex mutex;
            bool criado;
            try { mutex = new Mutex(true, @"Local\" + nome, out criado); }
            catch (Exception erro) when (erro is UnauthorizedAccessException || erro is WaitHandleCannotBeOpenedException || erro is IOException)
            {
                Log.Erro("Mutex indisponível; processando sozinho: " + primeiroArquivo, erro);
                return new Fila(null, nome, primeiroArquivo);
            }
            if (criado)
            {
                var fila = new Fila(mutex, nome, primeiroArquivo);
                fila._servidor = Task.Run(() => fila.Escutar());
                return fila;
            }
            mutex.Dispose();
            if (Entregar(nome, primeiroArquivo)) return null;
            Log.Info("Pipe indisponível; processando sozinho: " + primeiroArquivo);
            return new Fila(null, nome, primeiroArquivo);
        }

        public bool TentarPegar(int esperaMs, out string arquivo) => _itens.TryTake(out arquivo, esperaMs);

        static bool Entregar(string nome, string arquivo)
        {
            try
            {
                using (var cliente = new NamedPipeClientStream(".", nome, PipeDirection.Out))
                {
                    cliente.Connect(EsperaConexaoMs);
                    var bytes = Encoding.UTF8.GetBytes(arquivo);
                    cliente.Write(bytes, 0, bytes.Length);
                    cliente.Flush();
                    cliente.WaitForPipeDrain();
                    return true;
                }
            }
            catch (Exception erro) when (erro is TimeoutException || erro is IOException || erro is UnauthorizedAccessException)
            {
                Log.Erro("Falha ao entregar arquivo pelo pipe", erro);
                return false;
            }
        }

        async Task Escutar()
        {
            PipeSecurity seguranca;
            try { seguranca = SoUsuarioAtual(); }
            catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException || erro is System.Security.SecurityException)
            {
                Log.Erro("Falha ao definir permissões do pipe; seguindo sem servidor", erro);
                return;
            }
            while (!_parar.IsCancellationRequested)
            {
                try
                {
                    using (var servidor = new NamedPipeServerStream(_nome, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, seguranca))
                    {
                        // No net48, cancelar a operação e fechar o pipe pode disputar o mesmo SafeHandle.
                        using (_parar.Token.Register(servidor.Dispose))
                            await servidor.WaitForConnectionAsync().ConfigureAwait(false);
                        using (var limite = CancellationTokenSource.CreateLinkedTokenSource(_parar.Token))
                        {
                            limite.CancelAfter(LimiteConexaoMs);
                            // O fechamento do stream interrompe a leitura sem um segundo callback CancelIoEx.
                            using (limite.Token.Register(servidor.Dispose))
                            {
                                var arquivo = await Ler(servidor).ConfigureAwait(false);
                                if (_parar.IsCancellationRequested)
                                {
                                    // Mensagem completa que chegou durante o encerramento volta como pendente.
                                    if (!string.IsNullOrEmpty(arquivo)) Adicionar(arquivo);
                                    return;
                                }
                                if (limite.IsCancellationRequested)
                                    Log.Erro("Conexão do pipe excedeu " + LimiteConexaoMs + " ms; mensagem descartada");
                                else if (!string.IsNullOrEmpty(arquivo))
                                    Adicionar(arquivo);
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException erro)
                {
                    Log.Erro("Sem permissão para criar o pipe; seguindo sem servidor", erro);
                    return;
                }
                catch (OperationCanceledException) when (_parar.IsCancellationRequested) { return; }
                catch (OperationCanceledException) { Log.Info("Conexão do pipe expirou; descartada"); }
                catch (Exception erro) when (erro is IOException || erro is ObjectDisposedException)
                {
                    Log.Erro("Falha no servidor do pipe", erro);
                    if (!await Pausar().ConfigureAwait(false)) return;
                }
                catch (Exception erro)
                {
                    Log.Erro("Falha inesperada no servidor do pipe", erro);
                    if (!await Pausar().ConfigureAwait(false)) return;
                }
            }
        }

        // Só o usuário atual pode conectar ao pipe, e só localmente (conexões de rede são negadas).
        internal static PipeSecurity SoUsuarioAtual()
        {
            var seguranca = new PipeSecurity();
            seguranca.AddAccessRule(new PipeAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.NetworkSid, null),
                PipeAccessRights.FullControl, System.Security.AccessControl.AccessControlType.Deny));
            using (var identidade = System.Security.Principal.WindowsIdentity.GetCurrent())
                seguranca.AddAccessRule(new PipeAccessRule(identidade.User, PipeAccessRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
            return seguranca;
        }

        void Adicionar(string arquivo)
        {
            try { if (_itens.TryAdd(arquivo)) return; }
            catch (InvalidOperationException) { }
            Log.Erro("Vídeo chegou depois do encerramento: " + arquivo);
        }

        // Evita loop quente quando o pipe falha repetidamente. Falso = encerrando.
        async Task<bool> Pausar()
        {
            try { await Task.Delay(PausaErroMs, _parar.Token).ConfigureAwait(false); return true; }
            catch (OperationCanceledException) { return false; }
        }

        static async Task<string> Ler(Stream origem)
        {
            using (var memoria = new MemoryStream())
            {
                var buffer = new byte[4096];
                int lidos;
                while ((lidos = await origem.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None).ConfigureAwait(false)) > 0)
                {
                    memoria.Write(buffer, 0, lidos);
                    if (memoria.Length > LimiteMensagemBytes)
                    {
                        Log.Erro("Mensagem do pipe acima do limite; descartada");
                        return null;
                    }
                }
                return Encoding.UTF8.GetString(memoria.ToArray());
            }
        }

        // Para de escutar e devolve o que ainda não foi pego, para o chamador relançar.
        public IList<string> Encerrar()
        {
            _encerrada = true;
            _parar.Cancel();
            try { _servidor?.Wait(1000); } catch (AggregateException) { }
            _itens.CompleteAdding();
            var pendentes = new List<string>();
            while (_itens.TryTake(out var item)) pendentes.Add(item);
            return pendentes;
        }

        public void Dispose()
        {
            if (!_encerrada)
                foreach (var item in Encerrar()) Log.Erro("Vídeo ficou sem processamento: " + item);
            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
                _mutex.Dispose();
            }
            _parar.Dispose();
        }
    }
}

