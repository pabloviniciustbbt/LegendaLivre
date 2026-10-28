using System;

namespace LegendaLivre
{
    public sealed class Idioma
    {
        public static readonly Idioma PortuguesBrasil = new Idioma(6, "Português (Brasil)");
        public static readonly Idioma PortuguesPortugal = new Idioma(36, "Português (Portugal)");
        public static readonly Idioma Ingles = new Idioma(14, "Inglês");
        public static readonly Idioma[] Todos = { PortuguesBrasil, PortuguesPortugal, Ingles };

        public int Codigo { get; }
        public string Nome { get; }

        Idioma(int codigo, string nome)
        {
            Codigo = codigo;
            Nome = nome;
        }

        public static Idioma PorCodigo(int codigo) => Array.Find(Todos, i => i.Codigo == codigo);
    }
}
