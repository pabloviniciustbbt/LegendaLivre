using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Xml;

namespace LegendaLivre
{
    public sealed class Legenda
    {
        public string Nome { get; set; }
        public string Id { get; set; }
        public string Data { get; set; }
    }

    public sealed class Resposta
    {
        public byte[] Corpo { get; set; }
        public string TipoConteudo { get; set; }
    }

    public sealed class FalhaFonteException : Exception
    {
        public FalhaFonteException(string mensagem, Exception interna = null) : base(mensagem, interna) { }
    }

    public sealed class ClienteLegendas
    {
        public const string Base = "https://www.getsubtitle.com/webService/downloadManager/";
        const int TempoLimiteMs = 20000;
        const long LimiteCorpoBytes = 20 * 1024 * 1024;
        const int VersaoProtocolo = 383;
        static readonly Regex Identificador = new Regex(@"\A[0-9]+\z", RegexOptions.Compiled);

        readonly Func<string, Resposta> _obter;

        static ClienteLegendas()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public ClienteLegendas() : this(ObterComNovaTentativa) { }

        public ClienteLegendas(Func<string, Resposta> obter) => _obter = obter;

        public IList<Legenda> Listar(string hash, int codigoIdioma, bool melhor = false)
        {
            var consulta = melhor ? "get_best_subtitle_by_hash.php" : "get_subtitles_by_hash.php";
            var resposta = _obter($"{Base}{consulta}?version={VersaoProtocolo}&response=xml&cod_language={codigoIdioma}&hash={Codificar(hash)}");
            return InterpretarLista(Encoding.UTF8.GetString(resposta.Corpo ?? Array.Empty<byte>()), melhor);
        }

        public byte[] BaixarZip(Legenda legenda)
        {
            var resposta = _obter($"{Base}get_subtitle_files.php?version={VersaoProtocolo}&response=single&cod_bsplayer={Codificar(legenda.Id)}&post_date={Codificar(legenda.Data)}");
            var corpo = resposta.Corpo ?? Array.Empty<byte>();
            bool eZip = corpo.Length >= 4 && corpo[0] == 0x50 && corpo[1] == 0x4B;
            if (!eZip)
                throw new FalhaFonteException($"Download da legenda {legenda.Id} não devolveu zip ({resposta.TipoConteudo}, {corpo.Length} bytes)");
            return corpo;
        }

        public string ObterNomeFilme(string hash)
        {
            var resposta = _obter($"{Base}get_movie_by_hash.php?version={VersaoProtocolo}&response=xml&hash={Codificar(hash)}");
            var documento = LerDocumento(Encoding.UTF8.GetString(resposta.Corpo ?? Array.Empty<byte>()), "movie");
            if (documento == null) return null;
            try
            {
                var registro = documento.DocumentElement;
                var id = Decodificar(registro, "id");
                var nome = Decodificar(registro, "name");
                // Limite de tamanho: o nome alimenta uma regex em ServicoFilmes.
                return Identificador.IsMatch(id) && !string.IsNullOrWhiteSpace(nome) && nome.Length <= 300 ? nome.Trim() : null;
            }
            catch (FormatException erro) { throw new FalhaFonteException("Identificação de filme inválida", erro); }
        }

        static string Codificar(string texto) => Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(texto ?? "")));

        static string Decodificar(XmlNode registro, string campo) =>
            Encoding.UTF8.GetString(Convert.FromBase64String(registro.SelectSingleNode(campo)?.InnerText ?? ""));

        public static IList<Legenda> InterpretarLista(string texto, bool melhor = false)
        {
            var lista = new List<Legenda>();
            var documento = LerDocumento(texto, melhor ? "subtitle" : "subtitles");
            if (documento == null) return lista;
            foreach (XmlNode registro in documento.SelectNodes(melhor ? "/subtitle" : "/subtitles/subtitle"))
            {
                try
                {
                    var legenda = new Legenda { Nome = Decodificar(registro, "name"), Id = Decodificar(registro, "id"), Data = Decodificar(registro, "date") };
                    if (!Identificador.IsMatch(legenda.Id) || !DateTime.TryParseExact(legenda.Data, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                        || (!melhor && string.IsNullOrWhiteSpace(legenda.Nome)))
                        throw new FormatException("Campos obrigatórios inválidos");
                    lista.Add(legenda);
                }
                catch (FormatException erro) { Log.Info("Registro de legenda ignorado: " + erro.Message); }
            }
            return lista;
        }

        static XmlDocument LerDocumento(string texto, string raiz)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var documento = new XmlDocument { XmlResolver = null };
            try
            {
                var configuracao = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = LimiteCorpoBytes };
                using (var leitor = XmlReader.Create(new StringReader(texto), configuracao)) documento.Load(leitor);
            }
            catch (XmlException erro) { throw new FalhaFonteException("Fonte devolveu XML inválido", erro); }
            if (documento.DocumentElement?.Name != raiz)
                throw new FalhaFonteException("Fonte devolveu uma resposta inesperada");
            return documento;
        }

        static Resposta ObterComNovaTentativa(string url)
        {
            try { return Obter(url); }
            catch (FalhaFonteException primeira)
            {
                Log.Erro("Primeira tentativa falhou: " + url, primeira);
                return Obter(url);
            }
        }

        static Resposta Obter(string url)
        {
            try
            {
                // A URL lógica conserva os parâmetros; na rede eles seguem somente no corpo POST.
                var endereco = new Uri(url);
                var pedido = (HttpWebRequest)WebRequest.Create(endereco.GetLeftPart(UriPartial.Path));
                pedido.Method = "POST";
                pedido.Timeout = TempoLimiteMs;
                pedido.ReadWriteTimeout = TempoLimiteMs;
                pedido.UserAgent = "LegendaLivre/" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                pedido.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                pedido.ContentType = "application/x-www-form-urlencoded";
                var parametros = Encoding.ASCII.GetBytes(endereco.Query.TrimStart('?'));
                pedido.ContentLength = parametros.Length;
                using (var envio = pedido.GetRequestStream()) envio.Write(parametros, 0, parametros.Length);
                using (var resposta = (HttpWebResponse)pedido.GetResponse())
                using (var fluxo = resposta.GetResponseStream())
                {
                    // Um redirecionamento para http tiraria a proteção do TLS.
                    if (endereco.Scheme == Uri.UriSchemeHttps && resposta.ResponseUri.Scheme != Uri.UriSchemeHttps)
                        throw new FalhaFonteException("Resposta fora de HTTPS: " + url);
                    byte[] corpo;
                    try { corpo = LerComLimite(fluxo, LimiteCorpoBytes); }
                    catch (FalhaFonteException) { throw new FalhaFonteException("Resposta maior que 20 MB: " + url); }
                    return new Resposta { Corpo = corpo, TipoConteudo = resposta.ContentType ?? "" };
                }
            }
            catch (Exception erro) when (erro is WebException || erro is IOException || erro is ProtocolViolationException
                                         || erro is NotSupportedException || erro is InvalidOperationException)
            {
                throw new FalhaFonteException("Falha de rede em " + url, erro);
            }
        }

        internal static byte[] LerComLimite(Stream fluxo, long limite)
        {
            using (var memoria = new MemoryStream())
            {
                var bloco = new byte[8192];
                int lidos;
                while ((lidos = fluxo.Read(bloco, 0, bloco.Length)) > 0)
                {
                    if (memoria.Length + lidos > limite)
                        throw new FalhaFonteException("Resposta acima do limite de " + limite + " bytes");
                    memoria.Write(bloco, 0, lidos);
                }
                return memoria.ToArray();
            }
        }
    }
}
