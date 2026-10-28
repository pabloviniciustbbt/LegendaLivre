using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LegendaLivre
{
    internal static class MenuNativo
    {
        internal const string Clsid = "{a5b9460f-615c-49f8-88ec-13477de00ed9}";

        internal static string Instalar(string pasta)
        {
            var arquitetura = Arquitetura();
            byte[] conteudo;
            using (var recurso = typeof(MenuNativo).Assembly.GetManifestResourceStream($"LegendaLivre.MenuExplorer.{arquitetura}.dll"))
            using (var memoria = new MemoryStream())
            {
                if (recurso == null) throw new IOException("O pacote não contém o menu nativo para " + arquitetura);
                recurso.CopyTo(memoria);
                conteudo = memoria.ToArray();
            }
            var hash = Hash(conteudo);
            var caminho = Path.Combine(pasta, $"LegendaLivre.MenuExplorer-{arquitetura}-{hash.Substring(0, 24)}.dll");
            Directory.CreateDirectory(pasta);
            // Nome por conteúdo: uma atualização nunca sobrescreve uma DLL carregada no Explorer.
            if (File.Exists(caminho))
            {
                if (Hash(File.ReadAllBytes(caminho)) != hash) throw new IOException("O módulo do menu instalado tem conteúdo divergente.");
                return caminho;
            }
            using (var arquivo = new FileStream(caminho, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                arquivo.Write(conteudo, 0, conteudo.Length);
            return caminho;
        }

        internal static string Arquitetura()
        {
            GetNativeSystemInfo(out var sistema);
            switch (sistema.Arquitetura)
            {
                case 0: return "x86";
                case 9: return "x64";
                case 12: return "arm64";
                default: throw new IOException("Arquitetura do Windows não suportada pelo menu nativo.");
            }
        }

        internal static void RetirarModulos(string pasta, string temporaria)
        {
            foreach (var modulo in Directory.GetFiles(pasta, "LegendaLivre.MenuExplorer-*.dll"))
            {
                if (!Regex.IsMatch(Path.GetFileName(modulo), @"^LegendaLivre\.MenuExplorer-(x86|x64|arm64)-[0-9a-f]{24}\.dll$")) continue;
                try { File.Delete(modulo); }
                catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException)
                {
                    // O Windows permite mover o módulo carregado; ele continua disponível até o Explorer liberá-lo.
                    Directory.CreateDirectory(temporaria);
                    File.Move(modulo, Path.Combine(temporaria, Path.GetFileName(modulo)));
                }
            }
        }

        static string Hash(byte[] conteudo)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(conteudo)).Replace("-", "").ToLowerInvariant();
        }

        [StructLayout(LayoutKind.Sequential)]
        struct InformacoesSistema
        {
            public ushort Arquitetura, Reservado;
            public uint TamanhoPagina;
            public IntPtr EnderecoMinimo, EnderecoMaximo;
            public UIntPtr ProcessadoresAtivos;
            public uint QuantidadeProcessadores, TipoProcessador, Granularidade;
            public ushort NivelProcessador, RevisaoProcessador;
        }

        [DllImport("kernel32.dll")]
        static extern void GetNativeSystemInfo(out InformacoesSistema sistema);
    }
}
