using System;
using System.IO;
using System.Text;
static class ProgramaFalso
{
    static int Main(string[] argumentos)
    {
        var pasta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "invocacoes");
        Directory.CreateDirectory(pasta);
        var linhas = Array.ConvertAll(argumentos, a => Convert.ToBase64String(Encoding.UTF8.GetBytes(a)));
        File.WriteAllLines(Path.Combine(pasta, Guid.NewGuid().ToString("N") + ".txt"), linhas);
        return argumentos.Length == 3 ? 0 : 2;
    }
}
