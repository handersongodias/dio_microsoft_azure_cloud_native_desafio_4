using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Google.Protobuf.Reflection.UninterpretedOption.Types;

// Define o namespace para a função de validação de boleto
namespace fnValidaBoleto
{
  // Declara a classe principal da função Azure
  public class Function1
  {
    private readonly ILogger<Function1> _logger;

    // Construtor da classe, injeta a interface ILogger para logging
    public Function1(ILogger<Function1> logger)
    {
      _logger = logger;
    }

    // Define a função HTTP acionada por uma requisição POST
    [Function("barcode-validate")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
      // Lê o corpo da requisição HTTP de forma assíncrona
      string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
      // Desserializa o JSON do corpo da requisição para um objeto dinâmico
      dynamic data = JsonConvert.DeserializeObject(requestBody);
      // Extrai o valor do campo 'barcode' do JSON
      string barcodeData = data?.barcode;

      // Verifica se o campo 'barcode' está vazio ou nulo
      if (string.IsNullOrEmpty(barcodeData))
      {
        // Retorna um erro 400 Bad Request se o campo for obrigatório
        return new BadRequestObjectResult("O campo barcode é obrigatório");
      }
      // Verifica se o comprimento do código de barras é diferente de 44 caracteres
      if (barcodeData.Length != 44)
      {
        // Cria um objeto de resultado indicando que o boleto não é válido e a mensagem de erro
        var result = new { valido = false, mensagem = "O campo barcode deve ter 44 caracteres" };
        // Retorna um erro 400 Bad Request com a mensagem de erro
        return new BadRequestObjectResult(result);
      }

      // Extrai a parte da data do código de barras (caracteres 3 a 10)
      string datePart = barcodeData.Substring(3, 8);

      // Tenta fazer o parse da string da data para um objeto DateTime
      // O formato esperado é "yyyyMMdd"
      if (!DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime dateObj))
      {
        // Cria um objeto de resultado indicando que o boleto não é válido e a mensagem de erro
        var result = new { valido = false, mensagem = "Data de vencimento inválida" };
        return new BadRequestObjectResult(result);
      }

      // Cria um objeto de resultado indicando que o boleto é válido e a data de vencimento formatada
      var resultOK = new { valido = true, mensagem = "Boleto válido", vencimento = dateObj.ToString("dd-MM-yyyy") };

      // Retorna um status 200 OK com o objeto de resultado
      return new OkObjectResult(resultOK);
    }
  }
}
