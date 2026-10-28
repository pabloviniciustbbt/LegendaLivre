using System;
using System.Drawing;
using System.Windows.Forms;

namespace LegendaLivre
{
    public sealed class BarraTematica : Control
    {
        readonly Timer _animacao = new Timer { Interval = 40 };
        CoresTema _cores = CoresTema.Para(ModoTema.Claro);
        int _passo;

        public BarraTematica()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleRole = AccessibleRole.ProgressBar;
            TabStop = false;
            _animacao.Tick += (s, e) => { _passo = (_passo + 1) % 100; Invalidate(); };
        }

        internal void AplicarTema(CoresTema cores)
        {
            _cores = cores;
            BackColor = cores.Superficie;
            ForeColor = cores.Destaque;
            AtualizarAnimacao();
            Invalidate();
        }

        void AtualizarAnimacao() => _animacao.Enabled = Visible && IsHandleCreated &&
            _cores.Modo != ModoTema.AltoContraste && SystemInformation.UIEffectsEnabled;

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); AtualizarAnimacao(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); AtualizarAnimacao(); }
        protected override void OnHandleDestroyed(EventArgs e) { _animacao.Stop(); base.OnHandleDestroyed(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var largura = Math.Max(1, Width / 3);
            var inicio = _animacao.Enabled ? (Width + largura) * _passo / 100 - largura : (Width - largura) / 2;
            using (var pincel = new SolidBrush(ForeColor)) e.Graphics.FillRectangle(pincel, inicio, 0, largura, Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _animacao.Dispose();
            base.Dispose(disposing);
        }
    }
}
