using System;
using System.Text;

namespace LegendaLivre.Testes
{
    static class DadosFonte
    {
        public static string Lista(string registros, bool melhor = false)
        {
            var xml = new StringBuilder();
            foreach (var registro in (registros ?? "").Split('>'))
            {
                if (string.IsNullOrWhiteSpace(registro)) continue;
                int barra = registro.LastIndexOf('|');
                int data = registro.LastIndexOf('?');
                xml.Append("<subtitle>");
                if (barra >= 0 && data > barra)
                {
                    xml.Append(Campo("name", registro.Substring(0, barra)));
                    xml.Append(Campo("id", registro.Substring(barra + 1, data - barra - 1)));
                    xml.Append(Campo("date", registro.Substring(data + 1)));
                }
                xml.Append("</subtitle>");
                if (melhor) break;
            }
            return melhor ? (xml.Length == 0 ? "<subtitle/>" : xml.ToString()) : "<subtitles>" + xml + "</subtitles>";
        }

        static string Campo(string nome, string valor) => "<" + nome + ">" + Convert.ToBase64String(Encoding.UTF8.GetBytes(valor)) + "</" + nome + ">";
    }
}
