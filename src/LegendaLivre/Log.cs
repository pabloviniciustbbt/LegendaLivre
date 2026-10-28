using System;
using System.IO;

namespace LegendaLivre
{
    public static class Log
    {
        const long LimiteBytes = 1024 * 1024;
        static readonly object Trava = new object();

        public static string Caminho { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LegendaLivre", "log.txt");

        public static void Info(string mensagem) => Escrever("INFO", mensagem);

        public static void Erro(string mensagem, Exception erro = null) =>
            Escrever("ERRO", erro == null ? mensagem : mensagem + " | " + erro);

        static void Escrever(string nivel, string mensagem) => EscreverEm(Caminho, nivel, mensagem);

        internal static void EscreverEm(string caminho, string nivel, string mensagem)
        {
            try
            {
                lock (Trava)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(caminho));
                    var info = new FileInfo(caminho);
                    if (info.Exists && info.Length > LimiteBytes)
                    {
                        File.Copy(caminho, caminho + ".1", true);
                        File.Delete(caminho);
                    }
                    File.AppendAllText(caminho, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {nivel} {mensagem}{Environment.NewLine}");
                }
            }
            catch (Exception)
            {
                // Log nunca derruba o programa (outro processo pode estar escrevendo).
            }
        }
    }
}
