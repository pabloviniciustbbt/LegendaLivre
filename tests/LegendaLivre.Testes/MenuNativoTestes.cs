using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Xunit;

namespace LegendaLivre.Testes
{
    public sealed class MenuNativoTestes : IDisposable
    {
        readonly string _chave = @"Software\LegendaLivreMenuTeste-" + Guid.NewGuid().ToString("N");
        readonly string _pasta = Path.Combine(Path.GetTempPath(), "LegendaLivreMenu-" + Guid.NewGuid().ToString("N"));
        readonly RegistryKey _raiz;
        readonly Instalador _instalador;

        public MenuNativoTestes()
        {
            _raiz = Registry.CurrentUser.CreateSubKey(_chave);
            _instalador = new Instalador(_raiz, _pasta);
        }

        [Fact]
        public void RegistroNativo_CobreTodasAsExtensoesSemDuplicarMenuAntigo()
        {
            _instalador.RegistrarChaves(new Version(1, 3, 0));
            foreach (var ext in Instalador.Extensoes)
            {
                using (var nativo = _raiz.OpenSubKey($@"Software\Classes\SystemFileAssociations\.{ext}\shellex\ContextMenuHandlers\LegendaLivre"))
                    Assert.Equal(MenuNativo.Clsid, nativo?.GetValue(""));
                using (var anterior = _raiz.OpenSubKey($@"Software\Classes\SystemFileAssociations\.{ext}\shell\LegendaLivre"))
                    Assert.Equal("", anterior.GetValue("LegacyDisable"));
            }
            using (var servidor = _raiz.OpenSubKey($@"Software\Classes\CLSID\{MenuNativo.Clsid}\InprocServer32"))
            {
                Assert.Equal("Apartment", servidor.GetValue("ThreadingModel"));
                Assert.True(File.Exists((string)servidor.GetValue("")));
                Assert.Equal(_pasta, Path.GetDirectoryName((string)servidor.GetValue("")));
            }
        }

        [Theory]
        [InlineData("x64", 0x8664)]
        [InlineData("x86", 0x014c)]
        [InlineData("arm64", 0xaa64)]
        public void Pacote_ContemModuloNativoDaArquiteturaCorreta(string arquitetura, int maquina)
        {
            using (var recurso = typeof(Instalador).Assembly.GetManifestResourceStream($"LegendaLivre.MenuExplorer.{arquitetura}.dll"))
            using (var leitor = new BinaryReader(recurso))
            {
                Assert.Equal(0x5a4d, leitor.ReadUInt16());
                recurso.Position = 0x3c;
                recurso.Position = leitor.ReadUInt32();
                Assert.Equal(0x4550u, leitor.ReadUInt32());
                Assert.Equal(maquina, leitor.ReadUInt16());
            }
        }

        [Fact]
        public void Extracao_VerificaConteudoExistenteENaoSobrescreveModuloDiferente()
        {
            var modulo = MenuNativo.Instalar(_pasta);
            Assert.Equal(modulo, MenuNativo.Instalar(_pasta));
            File.WriteAllText(modulo, "conteudo divergente");
            Assert.Throws<IOException>(() => MenuNativo.Instalar(_pasta));
            Assert.Equal("conteudo divergente", File.ReadAllText(modulo));
        }

        [Fact]
        public void Remocao_LimpaRegistroNativoEPreservaOutroHandler()
        {
            const string caminho = @"Software\Classes\SystemFileAssociations\.mkv\shellex\ContextMenuHandlers\Outro";
            using (var outro = _raiz.CreateSubKey(caminho)) outro.SetValue("", "outro-componente");
            _instalador.RegistrarChaves(new Version(1, 3, 0));
            Assert.True(_instalador.RemoverChaves());
            using (var outro = _raiz.OpenSubKey(caminho)) Assert.Equal("outro-componente", outro.GetValue(""));
            using (var classe = _raiz.OpenSubKey($@"Software\Classes\CLSID\{MenuNativo.Clsid}")) Assert.Null(classe);
            foreach (var ext in Instalador.Extensoes)
                using (var removido = _raiz.OpenSubKey($@"Software\Classes\SystemFileAssociations\.{ext}\shellex\ContextMenuHandlers\LegendaLivre"))
                    Assert.Null(removido);
        }

        [Fact]
        public void Remocao_ModuloCarregadoPodeSairDaPastaSemEncerrarExplorer()
        {
            var modulo = MenuNativo.Instalar(_pasta);
            var biblioteca = LoadLibrary(modulo);
            Assert.NotEqual(IntPtr.Zero, biblioteca);
            var temporaria = Path.Combine(_pasta, "temporario");
            var alheio = Path.Combine(_pasta, "LegendaLivre.MenuExplorer-outro.dll");
            File.WriteAllText(alheio, "outro arquivo");
            try
            {
                MenuNativo.RetirarModulos(_pasta, temporaria);
                Assert.False(File.Exists(modulo));
                Assert.True(File.Exists(Path.Combine(temporaria, Path.GetFileName(modulo))));
                Assert.True(File.Exists(alheio));
            }
            finally { FreeLibrary(biblioteca); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(string caminho);
        [DllImport("kernel32.dll")]
        static extern bool FreeLibrary(IntPtr modulo);

        public void Dispose()
        {
            _raiz.Dispose();
            Registry.CurrentUser.DeleteSubKeyTree(_chave, false);
            if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true);
        }
    }
}
