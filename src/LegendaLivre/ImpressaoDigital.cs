using System;
using System.IO;

namespace LegendaLivre
{
    // Hash de 64 bits: tamanho do arquivo + soma de inteiros de 8 bytes dos primeiros e últimos 64 KB.
    public static class ImpressaoDigital
    {
        const int Bloco = 65536;

        public static string Calcular(string caminhoArquivo)
        {
            using (var arquivo = new FileStream(caminhoArquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long tamanho = arquivo.Length;
                ulong soma = (ulong)tamanho;
                unchecked
                {
                    soma += SomarBloco(arquivo, 0, (int)Math.Min(Bloco, tamanho));
                    // O protocolo soma a cauda somente quando há um bloco completo de 64 KB.
                    if (tamanho >= Bloco) soma += SomarBloco(arquivo, tamanho - Bloco, Bloco);
                }
                return soma.ToString("x16");
            }
        }

        static ulong SomarBloco(FileStream arquivo, long inicio, int tamanho)
        {
            var buffer = new byte[tamanho];
            arquivo.Position = inicio;
            int lidos = 0;
            while (lidos < tamanho)
            {
                int n = arquivo.Read(buffer, lidos, tamanho - lidos);
                if (n == 0) throw new EndOfStreamException();
                lidos += n;
            }
            ulong soma = 0;
            unchecked
            {
                for (int i = 0; i + 8 <= tamanho; i += 8) soma += BitConverter.ToUInt64(buffer, i);
            }
            return soma;
        }
    }
}
