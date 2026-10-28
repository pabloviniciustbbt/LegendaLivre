using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace LegendaLivre
{
    public enum Status { Salva, NaoEncontrada, FalhaLeitura, FalhaLista, FalhaDownload, FalhaGravacao }

    public sealed class ResultadoDownload
    {
        public Status Status { get; set; }
        public string Caminho { get; set; }
    }

    public sealed class ResultadoLista
    {
        public Status Status { get; set; }
        public IList<Legenda> Legendas { get; set; } = new List<Legenda>();
    }

    public sealed class ServicoLegendas
    {
        // O servidor devolve legendas aleatórias para o hash zerado (arquivo vazio).
        const string HashZerado = "0000000000000000";
        readonly ClienteLegendas _cliente;
        readonly Func<string, string> _calcularHash;

        public ServicoLegendas() : this(new ClienteLegendas(), ImpressaoDigital.Calcular) { }

        public ServicoLegendas(ClienteLegendas cliente, Func<string, string> calcularHash)
        {
            _cliente = cliente;
            _calcularHash = calcularHash;
        }

        public ResultadoLista Listar(string video, Idioma idioma, int maximo)
            => Buscar(video, idioma, maximo, false);

        ResultadoLista Buscar(string video, Idioma idioma, int maximo, bool melhor)
        {
            string hash;
            try { hash = _calcularHash(video); }
            catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException || erro is ArgumentException || erro is NotSupportedException || erro is System.Security.SecurityException)
            {
                Log.Erro("Falha ao ler vídeo: " + video, erro);
                return new ResultadoLista { Status = Status.FalhaLeitura };
            }
            if (hash == HashZerado) return new ResultadoLista { Status = Status.NaoEncontrada };
            try
            {
                var legendas = _cliente.Listar(hash, idioma.Codigo, melhor).Take(maximo).ToList();
                return new ResultadoLista { Status = legendas.Count == 0 ? Status.NaoEncontrada : Status.Salva, Legendas = legendas };
            }
            catch (FalhaFonteException erro)
            {
                Log.Erro("Falha ao obter lista: " + video, erro);
                return new ResultadoLista { Status = Status.FalhaLista };
            }
        }

        public ResultadoDownload BaixarMelhor(string video, Idioma idioma, CancellationToken cancelamento = default(CancellationToken))
        {
            cancelamento.ThrowIfCancellationRequested();
            var lista = Buscar(video, idioma, 1, true);
            return lista.Status == Status.Salva
                ? Baixar(video, lista.Legendas[0], false, cancelamento)
                : new ResultadoDownload { Status = lista.Status };
        }

        public ResultadoDownload Baixar(string video, Legenda legenda, bool alternativa = false, CancellationToken cancelamento = default(CancellationToken))
        {
            cancelamento.ThrowIfCancellationRequested();
            ArquivoLegenda arquivo;
            try
            {
                arquivo = Salvamento.ExtrairDoZip(_cliente.BaixarZip(legenda));
            }
            catch (Exception erro) when (erro is FalhaFonteException || erro is InvalidDataException)
            {
                Log.Erro("Falha ao baixar legenda " + legenda.Id, erro);
                return new ResultadoDownload { Status = Status.FalhaDownload };
            }
            if (arquivo == null)
            {
                Log.Erro("Zip sem legenda: " + legenda.Id);
                return new ResultadoDownload { Status = Status.FalhaDownload };
            }
            try
            {
                cancelamento.ThrowIfCancellationRequested();
                var caminho = Salvamento.Gravar(video, arquivo, alternativa);
                Log.Info("Legenda salva: " + caminho);
                return new ResultadoDownload { Status = Status.Salva, Caminho = caminho };
            }
            catch (Exception erro) when (erro is IOException || erro is UnauthorizedAccessException || erro is ArgumentException || erro is NotSupportedException || erro is System.Security.SecurityException)
            {
                Log.Erro("Falha ao gravar legenda de " + video, erro);
                return new ResultadoDownload { Status = Status.FalhaGravacao };
            }
        }

        public static string MensagemPara(Status status)
        {
            switch (status)
            {
                case Status.Salva: return Mensagens.Sucesso;
                case Status.NaoEncontrada: return Mensagens.NenhumaLegenda;
                case Status.FalhaLeitura: return Mensagens.FalhaLeitura;
                case Status.FalhaLista: return Mensagens.FalhaLista;
                case Status.FalhaDownload: return Mensagens.FalhaDownload;
                default: return Mensagens.FalhaGravacao;
            }
        }
    }
}
