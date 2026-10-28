namespace LegendaLivre
{
    public enum Acao { PerguntarInstalar, Instalar, Desinstalar, RemoverPasta, Melhor, Top }

    public sealed class Comando
    {
        public Acao Acao { get; private set; }
        public Idioma Idioma { get; private set; }
        public string Arquivo { get; private set; }
        public string Pasta { get; private set; }
        public int Pid { get; private set; }

        public static Comando Interpretar(string[] args)
        {
            if (args.Length == 0) return new Comando { Acao = Acao.PerguntarInstalar };
            switch (args[0])
            {
                case "--instalar":
                    return args.Length == 1 ? new Comando { Acao = Acao.Instalar } : null;
                case "--desinstalar":
                    return args.Length == 1 ? new Comando { Acao = Acao.Desinstalar } : null;
                case "--remover-pasta":
                    return args.Length == 3 && int.TryParse(args[2], out var pid)
                        ? new Comando { Acao = Acao.RemoverPasta, Pasta = args[1], Pid = pid }
                        : null;
                case "melhor":
                case "top":
                    if (args.Length != 3 || !int.TryParse(args[1], out var codigo)) return null;
                    var idioma = Idioma.PorCodigo(codigo);
                    if (idioma == null || string.IsNullOrWhiteSpace(args[2])) return null;
                    return new Comando { Acao = args[0] == "melhor" ? Acao.Melhor : Acao.Top, Idioma = idioma, Arquivo = args[2] };
                default:
                    return null;
            }
        }

        public static string LinhaDoMenu(string exe, string modo, int codigo) => $"\"{exe}\" {modo} {codigo} \"%1\"";
    }
}
