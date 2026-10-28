using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace LegendaLivre
{
    public sealed class ArquivoLegenda
    {
        public string Extensao { get; set; }
        public byte[] Bytes { get; set; }
    }

    public static class Salvamento
    {
        const long LimiteBytes = 10 * 1024 * 1024;
        static readonly string[] Extensoes = { ".srt", ".sub", ".ass", ".ssa", ".vtt" };
        static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

        public static ArquivoLegenda ExtrairDoZip(byte[] zip)
        {
            using (var memoria = new MemoryStream(zip))
            using (var arquivo = AbrirZip(memoria))
            {
                ZipArchiveEntry entrada;
                try { entrada = arquivo.Entries.FirstOrDefault(e => Extensoes.Contains(Path.GetExtension(e.Name).ToLowerInvariant())); }
                // No .NET Framework, nome de entrada com caractere inválido lança ArgumentException ao ler o diretório do zip.
                catch (ArgumentException erro) { throw new InvalidDataException("Zip com nome de entrada inválido", erro); }
                if (entrada == null) return null;
                if (entrada.Length > LimiteBytes) throw new InvalidDataException("Legenda maior que 10 MB: " + entrada.Name);
                try
                {
                    using (var fluxo = entrada.Open())
                    using (var destino = new MemoryStream())
                    {
                        // O tamanho declarado no zip pode mentir: o limite vale durante a leitura.
                        var buffer = new byte[81920];
                        int lidos;
                        while ((lidos = fluxo.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            destino.Write(buffer, 0, lidos);
                            if (destino.Length > LimiteBytes) throw new InvalidDataException("Legenda maior que 10 MB: " + entrada.Name);
                        }
                        return new ArquivoLegenda { Extensao = Path.GetExtension(entrada.Name).ToLowerInvariant(), Bytes = destino.ToArray() };
                    }
                }
                catch (Exception erro) when (erro is NotSupportedException || (erro is IOException && !(erro is InvalidDataException)))
                {
                    throw new InvalidDataException("Entrada do zip ilegível: " + entrada.Name, erro);
                }
            }
        }

        // UTF-8 válido fica como está; o resto é lido como Windows-1252 e regravado em UTF-8 com BOM.
        public static byte[] ParaUtf8(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == Bom[0] && bytes[1] == Bom[1] && bytes[2] == Bom[2]) return bytes;
            try
            {
                new UTF8Encoding(false, true).GetString(bytes);
                return bytes;
            }
            catch (DecoderFallbackException) { }
            var texto = Encoding.GetEncoding(1252).GetString(bytes);
            return Bom.Concat(new UTF8Encoding(false).GetBytes(texto)).ToArray();
        }

        public static string Gravar(string caminhoVideo, ArquivoLegenda legenda, bool alternativa = false)
        {
            var pasta = Path.GetDirectoryName(caminhoVideo);
            var destino = Path.Combine(pasta, Path.GetFileNameWithoutExtension(caminhoVideo) + legenda.Extensao);
            var temporario = destino + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var criado = false;
            try
            {
                var bytes = ParaUtf8(legenda.Bytes);
                using (var fluxo = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    criado = true;
                    fluxo.Write(bytes, 0, bytes.Length);
                }
                if (alternativa)
                {
                    var nome = Path.Combine(pasta, Path.GetFileNameWithoutExtension(caminhoVideo));
                    for (var numero = 2; ; numero++)
                    {
                        destino = nome + ".legenda-" + numero + legenda.Extensao;
                        try { File.Move(temporario, destino); break; }
                        catch (IOException) when (File.Exists(destino)) { }
                    }
                }
                else if (File.Exists(destino))
                {
                    File.SetAttributes(destino, FileAttributes.Normal);
                    Substituir(temporario, destino, (t, d) => File.Replace(t, d, null));
                }
                else File.Move(temporario, destino);
            }
            catch
            {
                if (criado)
                    try { File.Delete(temporario); } catch (Exception) { }
                throw;
            }
            return destino;
        }

        // Se a substituição atômica falhar, copia por cima: nunca perde a legenda nova nem a antiga.
        internal static void Substituir(string temporario, string destino, Action<string, string> substituir)
        {
            try { substituir(temporario, destino); return; }
            catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException)
            {
                Log.Erro("Substituição atômica falhou; usando cópia", erro);
            }
            if (!File.Exists(destino)) File.Move(temporario, destino);
            else
            {
                File.Copy(temporario, destino, true);
                File.Delete(temporario);
            }
        }

        static ZipArchive AbrirZip(Stream fluxo)
        {
            try { return new ZipArchive(fluxo, ZipArchiveMode.Read); }
            catch (Exception erro) when (!(erro is InvalidDataException))
            {
                throw new InvalidDataException("Zip inválido", erro);
            }
        }
    }
}
