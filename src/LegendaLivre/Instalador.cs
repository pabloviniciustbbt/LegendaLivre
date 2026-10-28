using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LegendaLivre
{
    public enum ResultadoInstalacao { Instalado, Atualizado, JaAtualizado }

    public sealed class Instalador
    {
        public static readonly string[] Extensoes =
        {
            "3g2", "3gp", "3gp2", "3gpp", "ajp", "asf", "asx", "avchd", "avi", "bik", "bix", "box", "cam", "dat",
            "divx", "dmf", "evo", "flc", "fli", "flic", "flv", "flx", "gvi", "gvp", "h264", "m1v", "m2p", "m2ts",
            "m2v", "m4e", "m4v", "mjp", "mjpeg", "mjpg", "mkv", "moov", "mov", "movhd", "movie", "movx", "mp4",
            "mpe", "mpeg", "mpg", "mpv", "mpv2", "mxf", "nsv", "nut", "ogg", "ogm", "omf", "ram", "rmvb", "swf",
            "t5f", "tak", "vfw", "vid", "video", "viv", "vivo", "vob", "vro", "wmv", "wmx", "wrap", "wvx", "x264", "xvid",
        };

        const string Classes = @"Software\Classes";
        const string ChaveMenu = "LegendaLivre.Menu";
        const string ChaveDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LegendaLivre";
        static readonly (string chave, Idioma idioma)[] Submenus =
            { ("1_ptbr", Idioma.PortuguesBrasil), ("2_ptpt", Idioma.PortuguesPortugal), ("3_en", Idioma.Ingles) };

        readonly RegistryKey _raiz;

        public Instalador(RegistryKey raiz, string pastaDestino)
        {
            _raiz = raiz;
            PastaDestino = pastaDestino;
        }

        public static Instalador Padrao() => new Instalador(RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
            Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32), Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LegendaLivre"));

        public string PastaDestino { get; }
        public string CaminhoExe => Path.Combine(PastaDestino, "LegendaLivre.exe");

        public ResultadoInstalacao Instalar(string exeOrigem, Version versaoOrigem)
        {
            var instalada = VersaoInstalada();
            versaoOrigem = Normalizar(versaoOrigem);
            if (instalada != null && Normalizar(instalada) >= versaoOrigem && File.Exists(CaminhoExe) && MenuRegistrado()) return ResultadoInstalacao.JaAtualizado;
            Directory.CreateDirectory(PastaDestino);
            if (!string.Equals(Path.GetFullPath(exeOrigem), Path.GetFullPath(CaminhoExe), StringComparison.OrdinalIgnoreCase))
                File.Copy(exeOrigem, CaminhoExe, true);
            RegistrarChaves(versaoOrigem);
            Log.Info($"Instalação concluída: {versaoOrigem} em {PastaDestino}");
            return instalada == null ? ResultadoInstalacao.Instalado : ResultadoInstalacao.Atualizado;
        }

        internal static Version Normalizar(Version v) => new Version(v.Major, v.Minor, Math.Max(v.Build, 0));

        public void RegistrarChaves(Version versao)
        {
            var modulo = MenuNativo.Instalar(PastaDestino);
            using (var servidor = _raiz.CreateSubKey($@"{Classes}\CLSID\{MenuNativo.Clsid}\InprocServer32"))
            {
                servidor.SetValue("", modulo);
                servidor.SetValue("ThreadingModel", "Apartment");
            }
            foreach (var ext in Extensoes)
            {
                using (var nativo = _raiz.CreateSubKey($@"{Classes}\SystemFileAssociations\.{ext}\shellex\ContextMenuHandlers\LegendaLivre"))
                    nativo.SetValue("", MenuNativo.Clsid);
                using (var k = _raiz.CreateSubKey($@"{Classes}\SystemFileAssociations\.{ext}\shell\LegendaLivre"))
                {
                    k.SetValue("LegacyDisable", "");
                    k.SetValue("MUIVerb", "LegendaLivre");
                    k.SetValue("ExtendedSubCommandsKey", ChaveMenu);
                    k.SetValue("MultiSelectModel", "Player");
                    k.SetValue("Icon", CaminhoExe);
                }
            }

            foreach (var (chave, idioma) in Submenus)
            {
                using (var k = _raiz.CreateSubKey($@"{Classes}\{ChaveMenu}\shell\{chave}"))
                {
                    k.SetValue("MUIVerb", idioma.Nome);
                    k.SetValue("ExtendedSubCommandsKey", $"{ChaveMenu}.{idioma.Codigo}");
                    k.SetValue("MultiSelectModel", "Player");
                    k.DeleteValue("Icon", false);
                }
                CriarVerbo($@"{Classes}\{ChaveMenu}.{idioma.Codigo}\shell\1_melhor", "Melhor legenda", "melhor", idioma.Codigo);
                CriarVerbo($@"{Classes}\{ChaveMenu}.{idioma.Codigo}\shell\2_top", "Top 10", "top", idioma.Codigo);
            }

            using (var k = _raiz.CreateSubKey(ChaveDesinstalar))
            {
                k.SetValue("DisplayName", "LegendaLivre");
                k.SetValue("DisplayVersion", Normalizar(versao).ToString(3));
                k.SetValue("Publisher", "LegendaLivre");
                k.SetValue("DisplayIcon", CaminhoExe);
                k.SetValue("InstallLocation", PastaDestino);
                k.SetValue("UninstallString", $"\"{CaminhoExe}\" --desinstalar");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", 300, RegistryValueKind.DWord);
            }
        }

        void CriarVerbo(string caminho, string titulo, string modo, int codigo)
        {
            using (var k = _raiz.CreateSubKey(caminho))
            {
                k.SetValue("MUIVerb", titulo);
                k.SetValue("MultiSelectModel", "Player");
                k.DeleteValue("Icon", false);
                using (var cmd = k.CreateSubKey("command"))
                    cmd.SetValue("", Comando.LinhaDoMenu(CaminhoExe, modo, codigo));
            }
        }

        // Best-effort: uma chave que falha não impede as demais. Devolve true se tudo foi removido.
        public bool RemoverChaves()
        {
            var tudo = true;
            foreach (var ext in Extensoes)
            {
                var chaveExt = $@"{Classes}\SystemFileAssociations\.{ext}";
                tudo &= Tentar(() =>
                {
                    _raiz.DeleteSubKeyTree($@"{chaveExt}\shell\LegendaLivre", false);
                    _raiz.DeleteSubKeyTree($@"{chaveExt}\shellex\ContextMenuHandlers\LegendaLivre", false);
                    ApagarSeVazia(chaveExt + @"\shellex\ContextMenuHandlers");
                    ApagarSeVazia(chaveExt + @"\shellex");
                    ApagarSeVazia(chaveExt + @"\shell");
                    ApagarSeVazia(chaveExt);
                }, chaveExt);
            }
            tudo &= Tentar(() => _raiz.DeleteSubKeyTree($@"{Classes}\{ChaveMenu}", false), ChaveMenu);
            foreach (var idioma in Idioma.Todos)
                tudo &= Tentar(() => _raiz.DeleteSubKeyTree($@"{Classes}\{ChaveMenu}.{idioma.Codigo}", false), ChaveMenu + "." + idioma.Codigo);
            tudo &= Tentar(() => _raiz.DeleteSubKeyTree(ChaveDesinstalar, false), ChaveDesinstalar);
            tudo &= Tentar(() => _raiz.DeleteSubKeyTree($@"{Classes}\CLSID\{MenuNativo.Clsid}", false), MenuNativo.Clsid);
            Log.Info(tudo ? "Chaves do registro removidas" : "Chaves do registro removidas parcialmente");
            return tudo;
        }

        static bool Tentar(Action acao, string chave)
        {
            try { acao(); return true; }
            catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException || erro is System.Security.SecurityException)
            {
                Log.Erro("Falha ao remover a chave " + chave, erro);
                return false;
            }
        }

        // Só apaga a chave se ela não tiver subchaves nem valores (nunca mexe em conteúdo de outro programa).
        void ApagarSeVazia(string caminho)
        {
            using (var k = _raiz.OpenSubKey(caminho))
                if (k == null || k.SubKeyCount > 0 || k.ValueCount > 0) return;
            _raiz.DeleteSubKey(caminho, false);
        }

        public Version VersaoInstalada()
        {
            using (var k = _raiz.OpenSubKey(ChaveDesinstalar))
                return k?.GetValue("DisplayVersion") is string v && Version.TryParse(v, out var versao) ? versao : null;
        }

        bool MenuRegistrado()
        {
            using (var servidor = _raiz.OpenSubKey($@"{Classes}\CLSID\{MenuNativo.Clsid}\InprocServer32"))
                return servidor?.GetValue("") is string caminho && File.Exists(caminho);
        }

        public static void AvisarExplorer() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int evento, int flags, IntPtr item1, IntPtr item2);
    }
}
