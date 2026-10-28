using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace LegendaLivre
{
    static class Programa
    {
        const int EsperaJuntarMs = 500;
        static Version Versao => Assembly.GetExecutingAssembly().GetName().Version;
        static string ExeAtual => Assembly.GetExecutingAssembly().Location;

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => Log.Erro("Erro inesperado na interface", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Erro("Erro inesperado", e.ExceptionObject as Exception);

            var comando = Comando.Interpretar(args);
            if (comando == null)
            {
                Log.Erro("Argumentos inválidos: " + string.Join(" ", args));
                JanelaAviso.Mostrar(Mensagens.ArgumentosInvalidos, Mensagens.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }
            switch (comando.Acao)
            {
                case Acao.PerguntarInstalar:
                    if (JanelaAviso.Mostrar(Mensagens.PerguntaInstalar, Mensagens.Titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
                    return Instalar();
                case Acao.Instalar: return Instalar();
                case Acao.Desinstalar: return Desinstalar();
                case Acao.RemoverPasta: return RemoverPasta(comando.Pasta, comando.Pid);
                case Acao.Melhor: return Melhor(comando);
                default: return Top(comando);
            }
        }

        static int Instalar()
        {
            var instalador = Instalador.Padrao();
            try
            {
                var resultado = instalador.Instalar(ExeAtual, Versao);
                Instalador.AvisarExplorer();
                if (resultado == ResultadoInstalacao.Instalado)
                    using (var aviso = new JanelaAviso(Mensagens.InstaladoTitulo, Mensagens.InstaladoTexto, Mensagens.Fechar)) aviso.ShowDialog();
                else
                    JanelaAviso.Mostrar(string.Format(resultado == ResultadoInstalacao.Atualizado ? Mensagens.Atualizado : Mensagens.JaInstalado,
                        Instalador.Normalizar(resultado == ResultadoInstalacao.Atualizado ? Versao : instalador.VersaoInstalada()).ToString(3)),
                        Mensagens.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException || erro is System.Security.SecurityException)
            {
                Log.Erro("Falha ao instalar", erro);
                JanelaAviso.Mostrar(Mensagens.FalhaInstalar, Mensagens.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        // Remove as chaves e entrega a remoção da pasta a uma cópia em %TEMP%, que espera este processo sair.
        static int Desinstalar()
        {
            var instalador = Instalador.Padrao();
            var chavesRemovidas = instalador.RemoverChaves();
            Instalador.AvisarExplorer();
            if (!chavesRemovidas)
            {
                // Sem apagar a pasta: o menu ainda apontaria para um exe inexistente.
                JanelaAviso.Mostrar(Mensagens.FalhaRemoverMenu, Mensagens.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            var copia = Path.Combine(Path.GetTempPath(), "LegendaLivre-desinstalar.exe");
            try
            {
                int pidAtual;
                using (var atual = Process.GetCurrentProcess()) pidAtual = atual.Id;
                File.Copy(ExeAtual, copia, true);
                Process.Start(new ProcessStartInfo(copia, $"--remover-pasta \"{instalador.PastaDestino}\" {pidAtual}") { UseShellExecute = false });
            }
            catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException || erro is Win32Exception)
            {
                Log.Erro("Falha ao iniciar remoção da pasta", erro);
                JanelaAviso.Mostrar(string.Format(Mensagens.FalhaDesinstalar, instalador.PastaDestino), Mensagens.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return 0;
        }

        static string Normalizar(string caminho) => Path.GetFullPath(caminho).TrimEnd('\\');

        // Só apaga a pasta de instalação padrão (e a de log); qualquer outro alvo é recusado.
        static int RemoverPasta(string pasta, int pid)
        {
            var padrao = Instalador.Padrao().PastaDestino;
            bool igual;
            try { igual = string.Equals(Normalizar(pasta), Normalizar(padrao), StringComparison.OrdinalIgnoreCase); }
            catch (Exception erro) when (erro is ArgumentException || erro is NotSupportedException || erro is PathTooLongException)
            {
                igual = false;
            }
            if (!igual)
            {
                Log.Erro("Remoção recusada: pasta inesperada " + pasta);
                return 2;
            }
            try { using (var p = Process.GetProcessById(pid)) p.WaitForExit(10000); }
            catch (ArgumentException) { }
            var pastaLog = Path.GetDirectoryName(Log.Caminho);
            for (int tentativa = 0; tentativa < 5 && Directory.Exists(padrao); tentativa++)
            {
                try
                {
                    MenuNativo.RetirarModulos(padrao, Path.Combine(Path.GetTempPath(), "LegendaLivre-menu-" + Guid.NewGuid().ToString("N")));
                    Directory.Delete(padrao, true);
                    if (Directory.Exists(pastaLog)) Directory.Delete(pastaLog, true);
                }
                catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException)
                {
                    Thread.Sleep(1000);
                }
            }
            if (Directory.Exists(padrao))
            {
                Log.Erro("Não foi possível apagar a pasta " + padrao);
                JanelaAviso.Mostrar(string.Format(Mensagens.FalhaRemoverPasta, padrao), Mensagens.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            JanelaAviso.Mostrar(Mensagens.Desinstalado, Mensagens.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        // Relança o próprio exe para cada vídeo que chegou à fila e não chegou a ser processado.
        static void Relancar(string modo, int codigo, System.Collections.Generic.IList<string> pendentes)
        {
            foreach (var pendente in pendentes)
            {
                try
                {
                    Log.Info("Relançando vídeo pendente: " + pendente);
                    Process.Start(new ProcessStartInfo(ExeAtual, $"{modo} {codigo} \"{pendente}\"") { UseShellExecute = false });
                }
                catch (Exception erro) when (erro is Win32Exception || erro is IOException)
                {
                    Log.Erro("Falha ao relançar vídeo: " + pendente, erro);
                }
            }
        }

        static int Melhor(Comando comando)
        {
            var codigo = comando.Idioma.Codigo;
            System.Collections.Generic.IList<string> pendentes;
            using (var fila = Fila.Abrir(Fila.NomePara("melhor", codigo), comando.Arquivo))
            {
                if (fila == null) return 0;
                var servico = new ServicoLegendas();
                while (fila.TentarPegar(EsperaJuntarMs, out var video))
                {
                    try
                    {
                        var resultado = servico.BaixarMelhor(video, comando.Idioma);
                        if (resultado.Status != Status.Salva)
                            Log.Info("Resultado de " + video + ": " + ServicoLegendas.MensagemPara(resultado.Status));
                    }
                    catch (Exception erro) { Log.Erro("Falha ao processar vídeo: " + video, erro); }
                }
                pendentes = fila.Encerrar();
            }
            Relancar("melhor", codigo, pendentes);
            return 0;
        }

        static int Top(Comando comando)
        {
            var codigo = comando.Idioma.Codigo;
            System.Collections.Generic.IList<string> pendentes;
            using (var fila = Fila.Abrir(Fila.NomePara("top", codigo), comando.Arquivo))
            {
                if (fila == null) return 0;
                fila.TentarPegar(0, out var video);
                if (fila.TentarPegar(EsperaJuntarMs, out _))
                {
                    JanelaAviso.Mostrar(Mensagens.ApenasUmArquivo, Mensagens.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                Application.Run(new JanelaTop10(video, comando.Idioma, new ServicoLegendas()));
                pendentes = fila.Encerrar();
            }
            Relancar("top", codigo, pendentes);
            return 0;
        }
    }
}
