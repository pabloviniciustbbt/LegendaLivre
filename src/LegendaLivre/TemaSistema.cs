using System;
using System.Drawing;
using System.IO;
using System.Security;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LegendaLivre
{
    public enum ModoTema { Claro, Escuro, AltoContraste }

    public sealed class CoresTema
    {
        public ModoTema Modo { get; }
        public Color Fundo { get; }
        public Color Superficie { get; }
        public Color Texto { get; }
        public Color TextoSecundario { get; }
        public Color Borda { get; }
        public Color Destaque { get; }
        public Color TextoDestaque { get; }
        public Color Selecao { get; }
        public Color TextoSelecao { get; }

        CoresTema(ModoTema modo)
        {
            Modo = modo;
            if (modo == ModoTema.AltoContraste)
            {
                Fundo = Superficie = SystemColors.Window;
                Texto = TextoSecundario = SystemColors.WindowText;
                Borda = SystemColors.WindowText;
                Destaque = Selecao = SystemColors.Highlight;
                TextoDestaque = TextoSelecao = SystemColors.HighlightText;
            }
            else if (modo == ModoTema.Escuro)
            {
                Fundo = Color.FromArgb(32, 32, 32);
                Superficie = Color.FromArgb(45, 45, 45);
                Texto = Color.FromArgb(245, 245, 245);
                TextoSecundario = Color.FromArgb(190, 190, 190);
                Borda = Color.FromArgb(90, 90, 90);
                Destaque = Color.FromArgb(96, 205, 255);
                TextoDestaque = Color.FromArgb(0, 45, 65);
                Selecao = Color.FromArgb(38, 79, 120);
                TextoSelecao = Color.White;
            }
            else
            {
                Fundo = Color.FromArgb(243, 243, 243);
                Superficie = Color.White;
                Texto = Color.FromArgb(27, 27, 27);
                TextoSecundario = Color.FromArgb(90, 90, 90);
                Borda = Color.FromArgb(160, 160, 160);
                Destaque = Color.FromArgb(0, 95, 184);
                TextoDestaque = Color.White;
                Selecao = Color.FromArgb(0, 95, 184);
                TextoSelecao = Color.White;
            }
        }

        public static CoresTema Para(ModoTema modo) => new CoresTema(modo);
    }

    public static class TemaSistema
    {
        const string Preferencias = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static CoresTema Obter()
        {
            var altoContraste = SystemInformation.HighContrast;
            if (altoContraste) return CoresTema.Para(ModoTema.AltoContraste);
            object valor = null;
            try
            {
                using (var chave = Registry.CurrentUser.OpenSubKey(Preferencias))
                    valor = chave?.GetValue("AppsUseLightTheme");
            }
            catch (Exception erro) when (erro is SecurityException || erro is UnauthorizedAccessException || erro is IOException)
            {
                Log.Erro("Não foi possível consultar o tema do Windows; usando tema claro", erro);
            }
            return CoresTema.Para(EscolherModo(altoContraste, valor));
        }

        internal static ModoTema EscolherModo(bool altoContraste, object valor) =>
            altoContraste ? ModoTema.AltoContraste : valor is int inteiro && inteiro == 0 ? ModoTema.Escuro : ModoTema.Claro;
    }
}
