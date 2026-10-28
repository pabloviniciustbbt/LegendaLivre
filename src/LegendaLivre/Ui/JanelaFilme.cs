using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LegendaLivre
{
    public abstract class JanelaFilme : JanelaTematica
    {
        readonly Label _tituloFilme;
        readonly Label _anoFilme;
        readonly PictureBox _capa;
        readonly Label _semCapa;
        Task<DadosFilme> _consulta;
        bool _fechada;
        protected Panel Conteudo { get; }

        protected JanelaFilme(string video, bool carregarCapa)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = Mensagens.Titulo;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(860, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Padding = new Padding(28);
            var cabecalho = CriarCabecalho(Mensagens.Titulo);
            cabecalho.Dock = DockStyle.Top;
            cabecalho.Padding = new Padding(0, 0, 0, 24);
            var corpo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            corpo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
            corpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var lateral = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 28, 0) };
            _capa = new PictureBox { Name = "capa", Dock = DockStyle.Top, Height = 236, SizeMode = PictureBoxSizeMode.Zoom, Tag = "superficie", BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Capa do filme" };
            _semCapa = new Label { Text = "Capa indisponível", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Tag = "superficie" };
            _capa.Controls.Add(_semCapa);
            lateral.Controls.Add(_capa);
            var detalhes = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            var titulo = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(0, 0, 0, 24) };
            _tituloFilme = CriarTitulo("", 18f);
            _tituloFilme.Margin = Padding.Empty;
            _tituloFilme.MaximumSize = new Size(470, 0);
            _anoFilme = new Label { AutoSize = true, Anchor = AnchorStyles.None, Tag = "discreto", Margin = new Padding(8, 0, 0, 0) };
            titulo.Controls.AddRange(new Control[] { _tituloFilme, _anoFilme });
            Conteudo = new Panel { Dock = DockStyle.Fill };
            detalhes.Controls.Add(Conteudo);
            detalhes.Controls.Add(titulo);
            corpo.Controls.Add(lateral, 0, 0);
            corpo.Controls.Add(detalhes, 1, 0);
            Controls.Add(corpo);
            Controls.Add(cabecalho);
            DefinirFilme(ServicoFilmes.Identificar(video));
            if (carregarCapa && !string.IsNullOrEmpty(video)) Shown += async (s, e) => await CarregarFilme(video);
            ResumeLayout(false);
        }

        internal void DefinirFilme(DadosFilme filme)
        {
            _tituloFilme.Text = filme.Titulo;
            _anoFilme.Text = filme.Ano == null ? "" : "(" + filme.Ano + ")";
            _anoFilme.Visible = !string.IsNullOrEmpty(filme.Ano);
            if (filme.Imagem == null) return;
            var anterior = _capa.Image;
            _capa.Image = filme.Imagem;
            _semCapa.Visible = false;
            anterior?.Dispose();
        }

        async Task CarregarFilme(string video)
        {
            try
            {
                _consulta = Task.Run(() => new ServicoFilmes().Obter(video));
                var filme = await _consulta;
                if (_fechada || IsDisposed) { filme.Imagem?.Dispose(); return; }
                DefinirFilme(filme);
            }
            catch (Exception erro) { Log.Erro("Falha ao carregar dados do filme", erro); }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _fechada = true;
            if (_consulta != null)
                _consulta.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Imagem?.Dispose(); });
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _capa?.Image?.Dispose();
            base.Dispose(disposing);
        }
    }
}
