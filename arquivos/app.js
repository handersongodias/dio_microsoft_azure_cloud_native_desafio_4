document.getElementById('barcodeForm').addEventListener('submit', async function(e) {
    e.preventDefault();
    document.getElementById('error').textContent = '';
    document.getElementById('result').style.display = 'none';

    const dataVencimento = document.getElementById('dataVencimento').value;
    const valor = document.getElementById('valor').value;

    try {
        const response = await fetch('http://localhost:7058/api/barcode-generate', {
            method: 'POST',
            headers: {
                'Content-Type': 'text/plain'
            },
            body: JSON.stringify({
                dataVencimento,
                valor: Number(valor)
            })
        });

        if (!response.ok) {
            throw new Error('Erro ao gerar código de barras.');
        }

        const data = await response.json();
        console.log(data); // Para depuração

        document.getElementById('barcodeText').textContent = data.barcode || '';
        if (data.imageBase64) {
            document.getElementById('barcodeImage').src = `data:image/png;base64,${data.imageBase64}`;
            document.getElementById('barcodeImage').style.display = 'block';
        } else {
            document.getElementById('barcodeImage').style.display = 'none';
        }
        document.getElementById('result').style.display = 'block';
        // Habilita o botão de validação e reseta cor
        const validateBtn = document.getElementById('validateBtn');
        validateBtn.disabled = false;
        const barcodeTextDiv = document.getElementById('barcodeText');
        barcodeTextDiv.style.color = ''; // Limpa cor ao gerar novo código
    } catch (err) {
        document.getElementById('error').textContent = err.message || 'Erro inesperado.';
    }
});

// Validação do código de barras
document.getElementById('validateBtn').addEventListener('click', async function() {
    const barcode = document.getElementById('barcodeText').textContent.trim();
    const barcodeTextDiv = document.getElementById('barcodeText');
    barcodeTextDiv.style.color = ''; // reset cor
    document.getElementById('error').textContent = '';
    if (!barcode) return;

    try {
        const response = await fetch('http://localhost:7098/api/barcode-validate', {
            method: 'POST',
            headers: {
                'Content-Type': 'text/plain'
            },
            body: JSON.stringify({ barcode })
        });
        if (!response.ok) throw new Error('Erro ao validar código de barras.');
        const data = await response.json();
        // Aceita qualquer propriedade booleana true/false
        const isValid = data.valido ?? data.valid ?? data === true;
        if (isValid === true) {
            barcodeTextDiv.style.color = 'green';
        } else if (isValid === false) {
            barcodeTextDiv.style.color = 'red';
        } else {
            barcodeTextDiv.style.color = '';
        }
    } catch (err) {
        document.getElementById('error').textContent = err.message || 'Erro inesperado.';
    }
});