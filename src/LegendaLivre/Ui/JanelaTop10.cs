using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LegendaLivre
{
    public sealed class JanelaTop10 : JanelaFilme
    {
        readonly string _video;
        readonly ServicoLegendas _servico;
        readonly ListaTematica _lista;
        readonly Button _baixar;
        readonly Button _cancelar;
        readonly Label _estado;
        readonly Label _resumo;
        readonly BarraTematica _barra;
        readonly CancellationTokenSource _cancelamento = new CancellationTokenSource();
        readonly ImageList _alturaLinhas = new ImageList();
        readonly Bitmap _imagemLinha;
        bool _resultado;
        bool _ocupada;
        bool _recursosDescartados;

        public JanelaTop10(string video, IList<Legenda> legendas, ServicoLegendas servico) : this(video, servico, false)
        {
            Preencher(legendas);
        }

        public JanelaTop10(string video, Idioma idioma, ServicoLegendas servico) : this(video, servico, servico != null)
        {
            _estado.Text = Mensagens.Buscando;
            _ocupada = true;
            _barra.Visible = true;
            AtualizarAcao();
            Shown += async (s, e) => await Buscar(idioma);
        }

        JanelaTop10(string video, ServicoLegendas servico, bool carregarCapa) : base(video, carregarCapa)
        {
            SuspendLayout();
            _video = video;
            _servico = servico;
            _estado = CriarTitulo("Escolha as legendas");
            _estado.Dock = DockStyle.Top;
            _estado.Padding = new Padding(0, 0, 0, 12);
            _resumo = new Label { Dock = DockStyle.Top, Height = 32, Tag = "discreto", AutoSize = false };
            _barra = new BarraTematica { Dock = DockStyle.Top, Height = 6, Visible = false, AccessibleName = Mensagens.Baixando };
            _lista = new ListaTematica { Name = "lista", View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true,
                MultiSelect = false, HideSelection = false, CheckBoxes = true, HeaderStyle = ColumnHeaderStyle.None,
                ShowItemToolTips = true, Cartoes = true, BorderStyle = BorderStyle.None, AccessibleName = "Legendas disponíveis; Espaço marca ou desmarca" };
            _lista.Columns.Add(Mensagens.ColunaNome);
            _alturaLinhas.ImageSize = new Size(1, 52);
            _imagemLinha = new Bitmap(1, 52);
            _alturaLinhas.Images.Add(_imagemLinha);
            _lista.SmallImageList = _alturaLinhas;
            _lista.FontChanged += (s, e) => AtualizarAlturaLinhas();
            _lista.HandleCreated += (s, e) => AtualizarAlturaLinhas();
            _lista.ItemChecked += (s, e) => { if (!_ocupada && !_resultado) AtualizarAcao(); };
            _lista.ItemActivate += async (s, e) => { if (!_resultado) await Baixar(); };
            _lista.MouseUp += (s, e) =>
            {
                if (!_lista.CheckBoxes || e.Button != MouseButtons.Left) return;
                var item = _lista.HitTest(e.Location);
                if (item.Item != null && (item.Location & ListViewHitTestLocations.StateImage) == 0)
                    item.Item.Checked = !item.Item.Checked;
            };
            _lista.Resize += (s, e) => AjustarColunas();
            _baixar = new Button { Text = "Baixar Legenda", AutoSize = true, Tag = "principal" };
            _baixar.Click += async (s, e) =>
            {
                if (_resultado) { Close(); ReprodutorVideo.Abrir(_video); }
                else await Baixar();
            };
            _cancelar = new Button { Text = Mensagens.Cancelar, AutoSize = true };
            _cancelar.Click += (s, e) => Close();
            Conteudo.Controls.Add(_lista);
            Conteudo.Controls.Add(_resumo);
            Conteudo.Controls.Add(_barra);
            Conteudo.Controls.Add(_estado);
            Controls.Add(CriarRodape(_baixar, _cancelar));
            AcceptButton = _baixar;
            CancelButton = _cancelar;
            ActiveControl = _lista;
            PrepararAparencia();
            ResumeLayout(true);
        }

        void AjustarColunas()
        {
            var largura = Math.Max(100, _lista.ClientSize.Width - 4);
            if (_lista.Columns.Count == 2)
            {
                _lista.Columns[1].Width = (int)(136 * DeviceDpi / 96f);
                largura -= _lista.Columns[1].Width;
            }
            _lista.Columns[0].Width = Math.Max(80, largura);
        }

        void AtualizarAlturaLinhas()
        {
            var altura = Math.Max((int)(52 * DeviceDpi / 96f), _lista.Font.Height * 2 + (int)(12 * DeviceDpi / 96f));
            _alturaLinhas.ImageSize = new Size(1, Math.Min(256, altura));
        }

        void Preencher(IList<Legenda> legendas)
        {
            foreach (var legenda in legendas)
                _lista.Items.Add(new ListViewItem(legenda.Nome) { Tag = legenda, ToolTipText = legenda.Nome });
            if (_lista.Items.Count > 0)
            {
                _lista.Items[0].Selected = _lista.Items[0].Focused = _lista.Items[0].Checked = true;
            }
            AtualizarAcao();
        }

        void AtualizarAcao()
        {
            var quantidade = _lista.CheckedItems.Count;
            _baixar.Text = quantidade > 1 ? "Baixar Legendas" : "Baixar Legenda";
            _baixar.Enabled = !_ocupada && quantidade > 0;
            _resumo.Text = _ocupada ? "" : quantidade + (quantidade == 1 ? " legenda selecionada" : " legendas selecionadas");
        }

        async Task Buscar(Idioma idioma)
        {
            try
            {
                var resultado = await Task.Run(() => _servico.Listar(_video, idioma, 10));
                if (IsDisposed || _cancelamento.IsCancellationRequested) return;
                _ocupada = false;
                _barra.Visible = false;
                if (resultado.Status != Status.Salva) { MostrarFalha(ServicoLegendas.MensagemPara(resultado.Status)); return; }
                _estado.Text = "Escolha as legendas";
                Preencher(resultado.Legendas);
                _lista.Focus();
            }
            catch (Exception erro)
            {
                if (IsDisposed || _cancelamento.IsCancellationRequested) return;
                Log.Erro("Falha ao buscar legendas", erro);
                MostrarFalha(Mensagens.FalhaLista);
            }
        }

        async Task Baixar()
        {
            if (_ocupada || _resultado || !_baixar.Enabled || _servico == null) return;
            var legendas = _lista.CheckedItems.Cast<ListViewItem>().Select(i => (Legenda)i.Tag).ToList();
            if (legendas.Count == 0) return;
            var token = _cancelamento.Token;
            _ocupada = true;
            _baixar.Visible = false;
            _lista.CheckBoxes = false;
            _lista.Enabled = false;
            _lista.Items.Clear();
            _lista.Columns.Add(Mensagens.ColunaResultado);
            AjustarColunas();
            foreach (var legenda in legendas) _lista.Items.Add(new ListViewItem(new[] { legenda.Nome, "Aguardando" }));
            _estado.Text = legendas.Count == 1 ? "Baixando legenda..." : Mensagens.Baixando;
            _barra.Visible = true;
            var resultados = new List<ResultadoDownload>();
            var salvas = 0;
            try
            {
                for (var indice = 0; indice < legendas.Count; indice++)
                {
                    token.ThrowIfCancellationRequested();
                    _lista.Items[indice].SubItems[1].Text = "Baixando...";
                    _resumo.Text = indice + " de " + legendas.Count + " arquivos concluídos";
                    var resultado = await Task.Run(() => _servico.Baixar(_video, legendas[indice], salvas > 0, token), token);
                    if (IsDisposed || token.IsCancellationRequested) return;
                    resultados.Add(resultado);
                    if (resultado.Status == Status.Salva) salvas++;
                    _lista.Items[indice].SubItems[1].Text = resultado.Status == Status.Salva ? "Salva" : ServicoLegendas.MensagemPara(resultado.Status);
                }
                MostrarResultado(resultados);
            }
            catch (OperationCanceledException) { }
            catch (Exception erro)
            {
                if (IsDisposed || token.IsCancellationRequested) return;
                Log.Erro("Falha ao baixar legendas selecionadas", erro);
                MostrarResultado(resultados);
                _estado.Text = Mensagens.FalhaDownload;
            }
        }

        internal void MostrarResultado(IList<ResultadoDownload> resultados)
        {
            var salvas = resultados.Count(r => r.Status == Status.Salva);
            _resultado = true;
            _lista.CheckBoxes = false;
            _ocupada = false;
            _barra.Visible = false;
            _lista.Enabled = true;
            _estado.Text = salvas == 0 ? "Nenhuma legenda foi salva" : salvas == 1 ? "Legenda baixada" : "Legendas baixadas";
            _resumo.Text = salvas + (salvas == 1 ? " arquivo salvo na pasta do vídeo" : " arquivos salvos na pasta do vídeo");
            _baixar.Text = "Dar o play";
            _baixar.Enabled = true;
            _baixar.Visible = salvas == 1;
            _cancelar.Text = Mensagens.Fechar;
            AcceptButton = salvas == 1 ? _baixar : _cancelar;
        }

        void MostrarFalha(string texto)
        {
            _ocupada = false;
            _barra.Visible = false;
            _estado.Text = texto;
            _resumo.Text = "";
            _baixar.Visible = false;
            _cancelar.Text = Mensagens.Fechar;
            AcceptButton = _cancelar;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cancelamento.Cancel();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_recursosDescartados)
            {
                _recursosDescartados = true;
                _cancelamento.Cancel();
                _cancelamento.Dispose();
            }
            base.Dispose(disposing);
            if (disposing)
            {
                _alturaLinhas.Dispose();
                _imagemLinha?.Dispose();
            }
        }
    }
}
