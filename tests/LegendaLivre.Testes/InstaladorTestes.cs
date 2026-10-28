using System;
using System.IO;
using Microsoft.Win32;
using Xunit;

namespace LegendaLivre.Testes
{
    public class InstaladorTestes : IDisposable
    {
        readonly string _raizTeste = @"Software\LegendaLivreTeste-" + Guid.NewGuid().ToString("N");
        readonly RegistryKey _raiz;
        readonly string _pasta = Path.Combine(Path.GetTempPath(), "LegendaLivreInst-" + Guid.NewGuid());
        readonly string _exeOrigem;
        readonly Instalador _instalador;

        public InstaladorTestes()
        {
            _raiz = Registry.CurrentUser.CreateSubKey(_raizTeste);
            Directory.CreateDirectory(_pasta);
            _exeOrigem = Path.Combine(_pasta, "origem.exe");
            File.WriteAllText(_exeOrigem, "exe falso");
            _instalador = new Instalador(_raiz, Path.Combine(_pasta, "Programs", "LegendaLivre"));
        }

        public void Dispose()
        {
            _raiz.Dispose();
            try { Registry.CurrentUser.DeleteSubKeyTree(_raizTeste, false); }
            finally { Directory.Delete(_pasta, true); }
        }

        [Fact]
        public void Instalar_CopiaExeERegistraMenuParaTodasAsExtensoes()
        {
            Assert.Equal(ResultadoInstalacao.Instalado, _instalador.Instalar(_exeOrigem, new Version(1, 0, 0)));
            Assert.True(File.Exists(_instalador.CaminhoExe));
            Assert.Equal(70, Instalador.Extensoes.Length);
            foreach (var ext in Instalador.Extensoes)
                using (var k = _raiz.OpenSubKey($@"Software\Classes\SystemFileAssociations\.{ext}\shell\LegendaLivre"))
                {
                    Assert.NotNull(k);
                    Assert.Equal("LegendaLivre", k.GetValue("MUIVerb"));
                    Assert.Equal("LegendaLivre.Menu", k.GetValue("ExtendedSubCommandsKey"));
                    Assert.Equal(_instalador.CaminhoExe, k.GetValue("Icon"));
                }
        }

        [Fact]
        public void Instalar_CriaTresIdiomasComDoisModos()
        {
            _instalador.Instalar(_exeOrigem, new Version(1, 0, 0));
            using (var menu = _raiz.OpenSubKey(@"Software\Classes\LegendaLivre.Menu\shell"))
                Assert.Equal(new[] { "1_ptbr", "2_ptpt", "3_en" }, menu.GetSubKeyNames());
            using (var cmd = _raiz.OpenSubKey(@"Software\Classes\LegendaLivre.Menu.36\shell\1_melhor\command"))
                Assert.Equal($"\"{_instalador.CaminhoExe}\" melhor 36 \"%1\"", cmd.GetValue(""));
            using (var top = _raiz.OpenSubKey(@"Software\Classes\LegendaLivre.Menu.14\shell\2_top"))
            {
                Assert.Equal("Top 10", top.GetValue("MUIVerb"));
                Assert.Equal("Player", top.GetValue("MultiSelectModel"));
            }
        }

        [Fact]
        public void RegistrarChaves_IconeApenasNaEntradaPrincipalPreservaTitulosEComandos()
        {
            _instalador.RegistrarChaves(new Version(1, 0, 0));
            var idiomas = new[] { ("1_ptbr", 6, "Português (Brasil)"), ("2_ptpt", 36, "Português (Portugal)"), ("3_en", 14, "Inglês") };
            foreach (var (chave, codigo, nome) in idiomas)
            {
                using (var menu = _raiz.OpenSubKey($@"Software\Classes\LegendaLivre.Menu\shell\{chave}"))
                {
                    Assert.Equal(nome, menu.GetValue("MUIVerb"));
                    Assert.Equal($"LegendaLivre.Menu.{codigo}", menu.GetValue("ExtendedSubCommandsKey"));
                    Assert.Null(menu.GetValue("Icon"));
                }
                using (var acoes = _raiz.OpenSubKey($@"Software\Classes\LegendaLivre.Menu.{codigo}\shell"))
                    Assert.Equal(new[] { "1_melhor", "2_top" }, acoes.GetSubKeyNames());
                foreach (var (acao, titulo, modo) in new[] { ("1_melhor", "Melhor legenda", "melhor"), ("2_top", "Top 10", "top") })
                    using (var verbo = _raiz.OpenSubKey($@"Software\Classes\LegendaLivre.Menu.{codigo}\shell\{acao}"))
                    {
                        Assert.Equal(titulo, verbo.GetValue("MUIVerb"));
                        Assert.Null(verbo.GetValue("Icon"));
                        Assert.Equal("Player", verbo.GetValue("MultiSelectModel"));
                        using (var comando = verbo.OpenSubKey("command"))
                            Assert.Equal($"\"{_instalador.CaminhoExe}\" {modo} {codigo} \"%1\"", comando.GetValue(""));
                    }
            }
        }

        [Fact]
        public void RegistrarChaves_RemoveIconesDeSubmenusJaExistentes()
        {
            foreach (var chave in new[] { "1_ptbr", "2_ptpt", "3_en" })
                using (var menu = _raiz.CreateSubKey($@"Software\Classes\LegendaLivre.Menu\shell\{chave}"))
                    menu.SetValue("Icon", "icone-antigo.exe");
            foreach (var codigo in new[] { 6, 36, 14 })
                foreach (var acao in new[] { "1_melhor", "2_top" })
                    using (var verbo = _raiz.CreateSubKey($@"Software\Classes\LegendaLivre.Menu.{codigo}\shell\{acao}"))
                        verbo.SetValue("Icon", "icone-antigo.exe");

            _instalador.RegistrarChaves(new Version(1, 2, 1));

            foreach (var chave in new[] { "1_ptbr", "2_ptpt", "3_en" })
                using (var menu = _raiz.OpenSubKey($@"Software\Classes\LegendaLivre.Menu\shell\{chave}"))
                    Assert.Null(menu.GetValue("Icon"));
            foreach (var codigo in new[] { 6, 36, 14 })
                foreach (var acao in new[] { "1_melhor", "2_top" })
                    using (var verbo = _raiz.OpenSubKey($@"Software\Classes\LegendaLivre.Menu.{codigo}\shell\{acao}"))
                        Assert.Null(verbo.GetValue("Icon"));
            using (var principal = _raiz.OpenSubKey(@"Software\Classes\SystemFileAssociations\.mkv\shell\LegendaLivre"))
                Assert.Equal(_instalador.CaminhoExe, principal.GetValue("Icon"));
        }

        [Fact]
        public void Instalar_RegistraEmAplicativosInstalados()
        {
            _instalador.Instalar(_exeOrigem, new Version(1, 2, 3));
            using (var k = _raiz.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LegendaLivre"))
            {
                Assert.Equal("LegendaLivre", k.GetValue("DisplayName"));
                Assert.Equal("1.2.3", k.GetValue("DisplayVersion"));
                Assert.Equal(_instalador.CaminhoExe, k.GetValue("DisplayIcon"));
                Assert.Equal($"\"{_instalador.CaminhoExe}\" --desinstalar", k.GetValue("UninstallString"));
                Assert.Equal(1, k.GetValue("NoModify"));
            }
            Assert.Equal(new Version(1, 2, 3), _instalador.VersaoInstalada());
        }

        [Fact]
        public void Instalar_MesmaVersao_NaoFazNada()
        {
            _instalador.Instalar(_exeOrigem, new Version(1, 0, 0));
            File.WriteAllText(_exeOrigem, "outro conteúdo");
            Assert.Equal(ResultadoInstalacao.JaAtualizado, _instalador.Instalar(_exeOrigem, new Version(1, 0, 0)));
            Assert.Equal("exe falso", File.ReadAllText(_instalador.CaminhoExe));
        }

        [Fact]
        public void Instalar_VersaoNova_AtualizaPorCima()
        {
            _instalador.Instalar(_exeOrigem, new Version(1, 0, 0));
            File.WriteAllText(_exeOrigem, "versão nova");
            Assert.Equal(ResultadoInstalacao.Atualizado, _instalador.Instalar(_exeOrigem, new Version(1, 1, 0)));
            Assert.Equal("versão nova", File.ReadAllText(_instalador.CaminhoExe));
            Assert.Equal(new Version(1, 1, 0), _instalador.VersaoInstalada());
        }

        [Fact]
        public void RemoverChaves_NaoDeixaNadaNoRegistro()
        {
            _instalador.Instalar(_exeOrigem, new Version(1, 0, 0));
            Assert.True(_instalador.RemoverChaves());
            using (var assoc = _raiz.OpenSubKey(@"Software\Classes\SystemFileAssociations"))
                Assert.True(assoc == null || assoc.SubKeyCount == 0, "sobraram chaves vazias de extensão");
            using (var classes = _raiz.OpenSubKey(@"Software\Classes"))
                Assert.DoesNotContain(classes.GetSubKeyNames(), n => n.StartsWith("LegendaLivre"));
            using (var k = _raiz.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LegendaLivre"))
                Assert.Null(k);
            Assert.Null(_instalador.VersaoInstalada());
        }

        [Fact]
        public void Normalizar_VersaoComDuasPartes_NaoLanca()
        {
            Assert.Equal(new Version(1, 2, 0), Instalador.Normalizar(new Version(1, 2)));
            Assert.Equal(new Version(1, 2, 3), Instalador.Normalizar(new Version(1, 2, 3, 4)));
        }

        [Fact]
        public void Instalar_MultiSelectModelPlayerNasDuasCamadas()
        {
            _instalador.Instalar(_exeOrigem, new Version(1, 0, 0));
            foreach (var caminho in new[] { @"SystemFileAssociations\.mkv\shell\LegendaLivre", @"LegendaLivre.Menu\shell\1_ptbr", @"LegendaLivre.Menu\shell\2_ptpt", @"LegendaLivre.Menu\shell\3_en" })
                using (var k = _raiz.OpenSubKey(@"Software\Classes\" + caminho))
                    Assert.Equal("Player", k.GetValue("MultiSelectModel"));
        }

        [Fact]
        public void RemoverChaves_PreservaOutrosVerbosDoUsuario()
        {
            using (var outro = _raiz.CreateSubKey(@"Software\Classes\SystemFileAssociations\.mkv\shell\OutroPrograma")) outro.SetValue("", "x");
            _instalador.Instalar(_exeOrigem, new Version(1, 0, 0));
            _instalador.RemoverChaves();
            using (var k = _raiz.OpenSubKey(@"Software\Classes\SystemFileAssociations\.mkv\shell\OutroPrograma"))
                Assert.NotNull(k);
        }

        [Fact]
        public void Instalar_VersaoDoAssemblyCom4Partes_ReconheceMesmaVersao()
        {
            _instalador.Instalar(_exeOrigem, new Version(1, 0, 0, 0));
            Assert.Equal(ResultadoInstalacao.JaAtualizado, _instalador.Instalar(_exeOrigem, new Version(1, 0, 0, 0)));
        }

        [Fact]
        public void Instalar_VersaoCom2Partes_GravaTresPartes()
        {
            _instalador.Instalar(_exeOrigem, new Version(2, 1));
            using (var k = _raiz.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LegendaLivre"))
                Assert.Equal("2.1.0", k.GetValue("DisplayVersion"));
        }
    }
}
