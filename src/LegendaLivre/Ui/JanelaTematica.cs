using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LegendaLivre
{
    public abstract class JanelaTematica : Form
    {
        readonly Func<CoresTema> _obterTema;
        readonly List<Font> _fontes = new List<Font>();
        readonly Icon _icone;

        protected JanelaTematica() : this(TemaSistema.Obter) { }

        protected JanelaTematica(Func<CoresTema> obterTema)
        {
            _obterTema = obterTema;
            var fonte = new Font("Segoe UI", 10f);
            _fontes.Add(fonte);
            Font = fonte;
            _icone = IconePrograma.Criar();
            Icon = _icone;
        }

        protected Label CriarTitulo(string texto, float tamanho = 14f)
        {
            var fonte = new Font("Segoe UI", tamanho, FontStyle.Bold);
            _fontes.Add(fonte);
            return new Label { Text = texto, Font = fonte, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
        }

        protected void PrepararAparencia() => AplicarTema(_obterTema());

        protected Control CriarCabecalho(string texto)
        {
            var cabecalho = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 16) };
            var imagem = IconePrograma.CriarImagem();
            Disposed += (s, e) => imagem.Dispose();
            cabecalho.Controls.Add(new PictureBox { Image = imagem, Size = new Size(32, 32), SizeMode = PictureBoxSizeMode.StretchImage, AccessibleRole = AccessibleRole.None, Margin = new Padding(0, 0, 10, 0) });
            var titulo = CriarTitulo(texto);
            titulo.Anchor = AnchorStyles.None;
            titulo.Margin = Padding.Empty;
            cabecalho.Controls.Add(titulo);
            return cabecalho;
        }

        protected Panel CriarRodape(params Button[] botoes)
        {
            var rodape = new Panel { Dock = DockStyle.Bottom, Height = 52 };
            var linha = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty };
            for (var i = 0; i < botoes.Length; i++)
            {
                botoes[i].Dock = DockStyle.None;
                botoes[i].Margin = new Padding(0, 0, i == botoes.Length - 1 ? 0 : 8, 0);
                linha.Controls.Add(botoes[i]);
            }
            rodape.Controls.Add(linha);
            System.EventHandler centralizar = (s, e) => linha.Location = new Point(
                System.Math.Max(0, (rodape.ClientSize.Width - linha.Width) / 2),
                System.Math.Max(0, (rodape.ClientSize.Height - linha.Height) / 2));
            rodape.Resize += centralizar;
            linha.SizeChanged += centralizar;
            return rodape;
        }

        internal void AplicarTema(CoresTema cores)
        {
            SuspendLayout();
            AplicarCores(this, cores);
            ResumeLayout(true);
            if (IsHandleCreated)
            {
                var escuro = cores.Modo == ModoTema.Escuro ? 1 : 0;
                // Windows antigos podem recusar o atributo; o conteúdo continua legível.
                DwmSetWindowAttribute(Handle, 20, ref escuro, sizeof(int));
            }
            Invalidate(true);
        }

        static void AplicarCores(Control controle, CoresTema cores)
        {
            controle.BackColor = cores.Fundo;
            controle.ForeColor = Equals(controle.Tag, "discreto") ? cores.TextoSecundario : cores.Texto;
            if (controle is ListaTematica lista) lista.AplicarTema(cores);
            else if (controle is BarraTematica barra) barra.AplicarTema(cores);
            else if (controle is Button botao)
            {
                var principal = Equals(botao.Tag, "principal");
                botao.FlatStyle = cores.Modo == ModoTema.AltoContraste ? FlatStyle.System : FlatStyle.Flat;
                botao.UseVisualStyleBackColor = cores.Modo == ModoTema.AltoContraste;
                if (cores.Modo != ModoTema.AltoContraste)
                {
                    botao.BackColor = principal ? cores.Destaque : cores.Superficie;
                    botao.ForeColor = principal ? cores.TextoDestaque : cores.Texto;
                    botao.FlatAppearance.BorderColor = principal ? cores.Destaque : cores.Borda;
                    botao.FlatAppearance.MouseOverBackColor = principal ? ControlPaint.Light(cores.Destaque, 0.1f) : cores.Fundo;
                    botao.FlatAppearance.MouseDownBackColor = principal ? cores.Destaque : cores.Superficie;
                }
                botao.MinimumSize = new Size((int)(112 * controle.DeviceDpi / 96f), (int)(36 * controle.DeviceDpi / 96f));
                botao.Padding = new Padding(8, 4, 8, 4);
                if (string.IsNullOrEmpty(botao.AccessibleName)) botao.AccessibleName = botao.Text.Replace("&", "");
            }
            else if (controle is ListView || Equals(controle.Tag, "superficie"))
            {
                controle.BackColor = cores.Superficie;
            }
            foreach (Control filho in controle.Controls) AplicarCores(filho, cores);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AplicarTema(_obterTema());
        }

        protected override void WndProc(ref Message mensagem)
        {
            base.WndProc(ref mensagem);
            if (!IsDisposed && !Disposing && IsHandleCreated &&
                (mensagem.Msg == 0x001A || mensagem.Msg == 0x0015 || mensagem.Msg == 0x031A))
                AplicarTema(_obterTema());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _icone?.Dispose();
                foreach (var fonte in _fontes) fonte.Dispose();
            }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr janela, int atributo, ref int valor, int tamanho);
    }
}
