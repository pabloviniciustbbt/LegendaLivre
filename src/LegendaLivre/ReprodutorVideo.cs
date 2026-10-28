using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace LegendaLivre
{
    internal static class ReprodutorVideo
    {
        // Só abre extensões de vídeo conhecidas: o caminho vem da linha de comando ou do pipe.
        internal static bool ExtensaoPermitida(string video) =>
            Array.IndexOf(Instalador.Extensoes, (video ?? "").Substring((video ?? "").LastIndexOf('.') + 1).ToLowerInvariant()) >= 0;

        internal static void Abrir(string video)
        {
            if (!ExtensaoPermitida(video))
            {
                Log.Erro("Abertura recusada: extensão não é de vídeo: " + video);
                return;
            }
            try { Process.Start(new ProcessStartInfo(video) { UseShellExecute = true }); }
            catch (Exception erro) when (erro is Win32Exception || erro is IOException)
            {
                Log.Erro("Falha ao abrir vídeo: " + video, erro);
            }
        }
    }
}
