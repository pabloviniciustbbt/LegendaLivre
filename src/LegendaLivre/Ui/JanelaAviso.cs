using System.Drawing;
using System.Windows.Forms;

namespace LegendaLivre
{
    public sealed class JanelaAviso : JanelaTematica
    {
        public JanelaAviso(string titulo, string texto, string textoBotao) : this(titulo, texto, textoBotao, false) { }

        internal JanelaAviso(string titulo, string texto, MessageBoxButtons botoes) : this(titulo, texto, "OK", botoes == MessageBoxButtons.YesNo) { }

        JanelaAviso(string titulo, string texto, string textoBotao, bool confirmar)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = titulo;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(24);

            var painel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Location = new Point(Padding.Left, Padding.Top), Margin = Padding.Empty };
            var linha = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            linha.Controls.Add(new Label { Text = texto, AutoSize = true, MaximumSize = new Size(400, 0), Margin = Padding.Empty });
            var botoes = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 20, 0, 0) };
            var botao = new Button { Text = confirmar ? "Sim" : textoBotao, AutoSize = true, DialogResult = confirmar ? DialogResult.Yes : DialogResult.OK, Tag = "principal", Margin = new Padding(0, 0, 8, 0) };
            botoes.Anchor = AnchorStyles.None;
            botao.Margin = Padding.Empty;
            botoes.Controls.Add(botao);
            painel.Controls.Add(CriarCabecalho(titulo));
            painel.Controls.AddRange(new Control[] { linha, botoes });
            Controls.Add(painel);
            AcceptButton = botao;
            CancelButton = botao;
            if (confirmar)
            {
                botao.Margin = new Padding(0, 0, 8, 0);
                var nao = new Button { Text = "Não", AutoSize = true, DialogResult = DialogResult.No, Margin = Padding.Empty };
                botoes.Controls.Add(nao);
                CancelButton = nao;
            }
            PrepararAparencia();
            ResumeLayout(false);
            PerformLayout();
        }

        public static DialogResult Mostrar(string texto, string titulo, MessageBoxButtons botoes, MessageBoxIcon icone, IWin32Window dono = null)
        {
            using (var janela = new JanelaAviso(titulo, texto, "OK", botoes == MessageBoxButtons.YesNo))
                return dono == null ? janela.ShowDialog() : janela.ShowDialog(dono);
        }
    }
}
