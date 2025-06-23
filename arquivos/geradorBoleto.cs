using Azure.Messaging.ServiceBus;
using BarcodeStandard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace fnGeradorBoletos
{
    public class GeradorCodigoBarras
    {
        private readonly ILogger<GeradorCodigoBarras> _logger;
        private readonly string _serviceBusConnectionString;
        // Nome da fila do Service Bus para onde as mensagens serão enviadas
        private readonly string _queueName = "gerador-codigo-barras";
        public GeradorCodigoBarras(ILogger<GeradorCodigoBarras> logger)
        {
            _logger = logger;
            // Obtém a string de conexão do Service Bus das variáveis de ambiente
            _serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
        }

        // Define a função HTTP acionada por uma requisição POST
        [Function("barcode-generate")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                // Lê o corpo da requisição HTTP
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                // Desserializa o JSON do corpo da requisição
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                // Extrai os valores de 'valor' e 'dataVencimento' do JSON
                string valor = data?.valor;
                string dataVencimento = data?.dataVencimento;

                string barcodeData;

                // Valida se os campos obrigatórios foram fornecidos
                if (string.IsNullOrEmpty(valor) || string.IsNullOrEmpty(dataVencimento))
                {
                    return new BadRequestObjectResult("Os campos valor e dataVencimento s�o obrigatorios!");
                }
                //validar formada data de vencimento
                if (!DateTime.TryParseExact(dataVencimento,"yyyy-MM-dd",null,System.Globalization.DateTimeStyles.None, out DateTime dateObj))
                {
                    return new BadRequestObjectResult("Data de vencimento inv�lida");
                }
                string dateStr = dateObj.ToString("yyyyMMdd");
                
                //Conversao do valor para centavos e formatação ate 8 digitos
                // Tenta converter o valor para decimal
                if(!decimal.TryParse(valor,out decimal valorDecimal))
                {
                    return new BadRequestObjectResult("Valor inválido");
                }
                // Converte o valor para centavos e formata com 8 dígitos
                int valorCentavos = (int)(valorDecimal * 10);
                string valorStr = valorCentavos.ToString("D8");

                // Define o código do banco e concatena com a data e o valor
                string bankCode = "008";
                string baseCode = string.Concat(bankCode, dateStr, valorStr);

                //Preenchimento do barCode para ter 44 caracteres
                // Preenche o código de barras com zeros à direita até 44 caracteres, se necessário
                barcodeData = baseCode.Length < 44 ? baseCode.PadRight(44, '0') : baseCode.Substring(0, 44);
                
                // Registra o código de barras gerado
                _logger.LogInformation($"Barcode gerado :{barcodeData}");

                // Gera a imagem do código de barras
                Barcode barcode = new Barcode();
                var skImage = barcode.Encode(BarcodeStandard.Type.Code128,barcodeData);
                // Codifica a imagem para PNG e obtém os bytes
                using (var encodeData = skImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                {
                    var imageBytes = encodeData.ToArray();
                    // Converte os bytes da imagem para uma string Base64
                    string base64String = Convert.ToBase64String(imageBytes);   

                    var resultObject = new
                    {
                        barcode = barcodeData,
                        valorOriginal = valorDecimal,
                        dataVencimento = DateTime.Now.AddDays(5),
                        ImageBase64 = base64String
                    }; 
                    // Envia o objeto resultante para a fila do Service Bus
                    await SendFileFallbackAsync(resultObject, _serviceBusConnectionString, _queueName);
                    // Retorna o objeto como resposta HTTP 200 OK
                    return new OkObjectResult(resultObject);
                }
            }
            catch (Exception)
            {
                // Em caso de erro, retorna um status 500 Internal Server Error
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
      
            
        }
        // Método para enviar a mensagem para a fila do Service Bus
        private async Task SendFileFallbackAsync(object resultObject, string serviceBusConnectionString, string queueName)
        {
            // Cria um cliente Service Bus
            await using var client = new ServiceBusClient(serviceBusConnectionString);
            // Cria um remetente para a fila especificada
            ServiceBusSender sender = client.CreateSender(queueName);
            // Serializa o objeto para JSON
            string messageBody = JsonConvert.SerializeObject(resultObject);
            // Cria uma nova mensagem Service Bus
            var message = new ServiceBusMessage(messageBody);
            // Envia a mensagem para a fila
            await sender.SendMessageAsync(message);
            // Registra que a mensagem foi enviada
            _logger.LogInformation($"Mensagem enviada para a fila {queueName}");
            
        }
    }
}
