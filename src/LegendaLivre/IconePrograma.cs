using System;
using System.Drawing;
using System.IO;

namespace LegendaLivre
{
    public static class IconePrograma
    {
        internal static Bitmap CriarImagem()
        {
            using (var recurso = typeof(IconePrograma).Assembly.GetManifestResourceStream("LegendaLivre.Assets.LegendaLivre.ico"))
            using (var leitor = new BinaryReader(recurso))
            {
                recurso.Position = 4;
                var quantidade = leitor.ReadUInt16();
                for (var i = 0; i < quantidade; i++)
                {
                    recurso.Position = 6 + 16 * i;
                    if (leitor.ReadByte() != 32) continue;
                    recurso.Position += 7;
                    var tamanho = leitor.ReadUInt32();
                    var posicao = leitor.ReadUInt32();
                    recurso.Position = posicao;
                    // Decodifica o quadro PNG diretamente: Icon.ToBitmap distorce este recurso no .NET Framework.
                    using (var quadro = new MemoryStream(leitor.ReadBytes((int)tamanho)))
                    using (var imagem = new Bitmap(quadro)) return new Bitmap(imagem);
                }
            }
            throw new InvalidOperationException("O ícone do programa não contém a imagem de 32 pixels.");
        }

        public static Icon Criar()
        {
            using (var recurso = typeof(IconePrograma).Assembly.GetManifestResourceStream("LegendaLivre.Assets.LegendaLivre.ico"))
            {
                if (recurso == null) throw new InvalidOperationException("O ícone do programa não foi incorporado ao executável.");
                using (var icone = new Icon(recurso))
                    return (Icon)icone.Clone();
            }
        }
    }
}
