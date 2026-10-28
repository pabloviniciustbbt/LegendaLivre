using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace LegendaLivre
{
    public sealed class ListaTematica : ListView
    {
        CoresTema _cores = CoresTema.Para(ModoTema.Claro);
        public bool Cartoes { get; set; }

        public ListaTematica()
        {
            DoubleBuffered = true;
            BorderStyle = BorderStyle.FixedSingle;
            HeaderStyle = ColumnHeaderStyle.Nonclickable;
            OwnerDraw = true;
        }

        internal void AplicarTema(CoresTema cores)
        {
            _cores = cores;
            BackColor = Cartoes || CheckBoxes ? cores.Fundo : cores.Superficie;
            ForeColor = cores.Texto;
            OwnerDraw = cores.Modo != ModoTema.AltoContraste;
            Invalidate();
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (var fundo = new SolidBrush(_cores.Fundo)) e.Graphics.FillRectangle(fundo, e.Bounds);
            using (var borda = new Pen(_cores.Borda))
                e.Graphics.DrawLine(borda, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var area = e.Bounds;
            area.Inflate(-(int)(8 * DeviceDpi / 96f), 0);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Font, area, _cores.TextoSecundario,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            base.OnDrawColumnHeader(e);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            if (View != View.Details) e.DrawDefault = true;
            base.OnDrawItem(e);
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            if (Cartoes || CheckBoxes)
            {
                DesenharOpcao(e);
                base.OnDrawSubItem(e);
                return;
            }
            var selecionado = e.Item.Selected;
            var fundo = selecionado ? _cores.Selecao : _cores.Superficie;
            var texto = selecionado ? _cores.TextoSelecao : _cores.Texto;
            using (var pincel = new SolidBrush(fundo)) e.Graphics.FillRectangle(pincel, e.Bounds);
            var area = e.Bounds;
            area.Inflate(-(int)(8 * DeviceDpi / 96f), 0);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Font, area, texto,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (e.ColumnIndex == 0 && e.Item.Focused && Focused)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -1, -1), texto, fundo);
            base.OnDrawSubItem(e);
        }

        void DesenharOpcao(DrawListViewSubItemEventArgs e)
        {
            var escala = DeviceDpi / 96f;
            var area = Rectangle.Inflate(new Rectangle(0, e.Bounds.Top, ClientSize.Width, e.Bounds.Height), -(int)(2 * escala), -(int)(4 * escala));
            var marcado = CheckBoxes && e.Item.Checked;
            var inicioTexto = e.Bounds.Left + (int)(14 * escala);
            if (e.ColumnIndex == 0)
            {
                using (var fundo = new SolidBrush(marcado ? _cores.Selecao : _cores.Superficie)) e.Graphics.FillRectangle(fundo, area);
                using (var borda = new Pen(marcado ? _cores.Destaque : _cores.Borda))
                    e.Graphics.DrawRectangle(borda, area.Left, area.Top, Math.Max(0, area.Width - 1), Math.Max(0, area.Height - 1));
                if (CheckBoxes)
                {
                    var estado = marcado ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
                    var tamanho = CheckBoxRenderer.GetGlyphSize(e.Graphics, estado);
                    var posicao = new Point(inicioTexto, area.Top + (area.Height - tamanho.Height) / 2);
                    CheckBoxRenderer.DrawCheckBox(e.Graphics, posicao, estado);
                    inicioTexto += tamanho.Width + (int)(12 * escala);
                }
            }
            var texto = new Rectangle(inicioTexto, area.Top + (int)(4 * escala),
                Math.Max(0, e.Bounds.Right - inicioTexto - (int)(14 * escala)), area.Height - (int)(8 * escala));
            var corTexto = e.ColumnIndex > 0 ? _cores.Destaque : marcado ? _cores.TextoSelecao : _cores.Texto;
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Font, texto, corTexto,
                TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (e.ColumnIndex == 0 && e.Item.Focused && Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(area, -2, -2));
        }
    }
}
