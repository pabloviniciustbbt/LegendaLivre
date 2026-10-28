using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LegendaLivre
{
    public sealed class DadosFilme
    {
        public string Titulo { get; set; }
        public string Ano { get; set; }
        public Bitmap Imagem { get; set; }
    }

    public sealed class ServicoFilmes
    {
        readonly Func<string, byte[]> _obter;
        readonly ClienteLegendas _cliente;
        public ServicoFilmes() : this(ObterBytes, new ClienteLegendas()) { }
        public ServicoFilmes(Func<string, byte[]> obter)
            : this(obter, new ClienteLegendas(url => new Resposta { Corpo = obter(url) })) { }
        ServicoFilmes(Func<string, byte[]> obter, ClienteLegendas cliente) { _obter = obter; _cliente = cliente; }

        public static DadosFilme Identificar(string video)
        {
            var nome = Path.GetFileNameWithoutExtension(video ?? "").Replace('_', ' ').Replace('.', ' ');
            string ano = null;
            foreach (Match data in Regex.Matches(nome, @"\b(?:18|19|20)\d{2}\b"))
                if (data.Index > 0 && nome.Substring(0, data.Index).Trim(' ', '(', '[', '-').Length > 0)
                {
                    ano = data.Value;
                    nome = nome.Substring(0, data.Index);
                    break;
                }
            nome = Regex.Split(nome, @"\b(?:S\d{1,2}E\d{1,3}|\d{3,4}p|BluRay|BDRip|BRRip|WEB|HDTV|DVDRip|x26[45]|HEVC)\b", RegexOptions.IgnoreCase)[0];
            nome = Regex.Replace(nome, @"\s+", " ").Trim(' ', '(', ')', '[', ']', '-');
            return new DadosFilme { Titulo = nome.Length == 0 ? "Arquivo de vídeo" : nome, Ano = ano };
        }

        public DadosFilme Obter(string video)
        {
            var filme = IdentificarNaFonte(video);
            try
            {
                var serie = Regex.IsMatch(video ?? "", @"S\d{1,2}E\d{1,3}", RegexOptions.IgnoreCase);
                var tipo = serie ? "series" : "movie";
                var url = "https://v3-cinemeta.strem.io/catalog/" + tipo + "/top/search=" + Uri.EscapeDataString(filme.Titulo) + ".json";
                var json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.DeserializeObject(Encoding.UTF8.GetString(_obter(url))) as Dictionary<string, object>;
                if (json == null || !json.TryGetValue("metas", out var registros) || !(registros is object[] metas)) return filme;
                var candidatos = metas.OfType<Dictionary<string, object>>().Where(registro =>
                    Normalizar(Texto(registro, "name")) == Normalizar(filme.Titulo)
                    && (filme.Ano == null || Regex.Match(Texto(registro, "releaseInfo"), @"^\d{4}").Value == filme.Ano)).ToList();
                if (candidatos.Count != 1) return filme;
                foreach (var registro in candidatos)
                {
                    var titulo = Texto(registro, "name");
                    var ano = Regex.Match(Texto(registro, "releaseInfo"), @"^\d{4}").Value;
                    if (Normalizar(titulo) != Normalizar(filme.Titulo) || (filme.Ano != null && filme.Ano != ano)) continue;
                    if (!Uri.TryCreate(Texto(registro, "poster"), UriKind.Absolute, out var capa)
                        || capa.Scheme != "https" || capa.Host != "m.media-amazon.com") continue;
                    using (var memoria = new MemoryStream(_obter(capa.AbsoluteUri)))
                    using (var imagem = new Bitmap(memoria))
                    {
                        if (imagem.Width > 2000 || imagem.Height > 3000) return filme;
                        filme.Imagem = new Bitmap(imagem);
                    }
                    filme.Titulo = titulo;
                    filme.Ano = ano.Length == 0 ? filme.Ano : ano;
                    break;
                }
            }
            catch (Exception erro) when (erro is WebException || erro is IOException || erro is ArgumentException
                || erro is InvalidOperationException || erro is OutOfMemoryException || erro is FalhaFonteException)
            {
                Log.Erro("Não foi possível obter a capa do filme", erro);
            }
            return filme;
        }

        DadosFilme IdentificarNaFonte(string video)
        {
            var filme = Identificar(video);
            try
            {
                var hash = ImpressaoDigital.Calcular(video);
                if (hash == "0000000000000000") return filme;
                var nome = _cliente.ObterNomeFilme(hash);
                if (string.IsNullOrWhiteSpace(nome)) return filme;
                var titulo = Regex.Match(nome, @"^(?<titulo>.+?)(?:\s+\((?<ano>\d{4})\))?$");
                if (titulo.Success)
                {
                    filme.Titulo = titulo.Groups["titulo"].Value;
                    filme.Ano = titulo.Groups["ano"].Success ? titulo.Groups["ano"].Value : null;
                }
            }
            catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException || erro is ArgumentException
                || erro is NotSupportedException || erro is System.Security.SecurityException || erro is FalhaFonteException)
            {
                Log.Erro("Não foi possível identificar o filme pela impressão digital", erro);
            }
            return filme;
        }

        static string Texto(Dictionary<string, object> registro, string chave) =>
            registro.TryGetValue(chave, out var valor) ? valor as string ?? "" : "";

        static string Normalizar(string titulo) => Regex.Replace(
            Regex.Replace((titulo ?? "").ToLowerInvariant(), @"^(the|a|an|o|os|as)\s+", ""), @"[^\p{L}\p{Nd}]", "");

        static byte[] ObterBytes(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var pedido = (HttpWebRequest)WebRequest.Create(url);
            pedido.Timeout = pedido.ReadWriteTimeout = 6000;
            pedido.AllowAutoRedirect = false;
            pedido.UserAgent = "LegendaLivre/" + typeof(ServicoFilmes).Assembly.GetName().Version.ToString(3);
            using (var resposta = (HttpWebResponse)pedido.GetResponse())
            using (var fluxo = resposta.GetResponseStream())
                return ClienteLegendas.LerComLimite(fluxo, url.EndsWith(".json", StringComparison.Ordinal) ? 1024 * 1024 : 4 * 1024 * 1024);
        }
    }
}
