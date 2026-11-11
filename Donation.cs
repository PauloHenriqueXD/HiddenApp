using System.Text;
using QRCoder;

namespace HiddenApp;

/// <summary>
/// Doações por Pix. O código "Pix Copia e Cola" (BR Code, padrão EMV do Banco Central) é montado
/// aqui, dentro do executável — nunca baixado da internet — para que ninguém consiga trocar a chave
/// sem publicar uma versão nova pelo GitHub oficial.
/// </summary>
internal static class Donation
{
    /// <summary>Chave Pix aleatória usada só para doações.</summary>
    public const string PixKey = "e4631919-5794-44d5-b5ed-5ebe8c9b5ac8";

    /// <summary>Nome completo que o banco de quem doa deve mostrar como recebedor.</summary>
    public const string ReceiverFullName = "Paulo Henrique Ferrari Mantuaneli";

    public const string OfficialSite = "github.com/PauloHenriqueXD/HiddenApp";

    /// <summary>Para quem está fora do Brasil (Pix só funciona entre contas brasileiras).</summary>
    public const string SponsorsUrl = "https://github.com/sponsors/PauloHenriqueXD";

    // Campos do BR Code: sem acentos, nome até 25 caracteres e cidade até 15 (regras do Pix)
    const string MerchantName = "PAULO H F MANTUANELI";
    const string MerchantCity = "TIETE";

    /// <summary>Código "Pix Copia e Cola" completo (com CRC).</summary>
    public static string Payload { get; } = BuildPayload();

    static string Field(string id, string value) => id + value.Length.ToString("00") + value;

    static string BuildPayload()
    {
        string account = Field("00", "br.gov.bcb.pix") + Field("01", PixKey);
        string payload =
            Field("00", "01") +                 // versão do formato
            Field("26", account) +              // dados da conta Pix
            Field("52", "0000") +               // categoria (não informada)
            Field("53", "986") +                // moeda: real
            Field("58", "BR") +                 // país
            Field("59", MerchantName) +         // nome do recebedor
            Field("60", MerchantCity) +         // cidade
            Field("62", Field("05", "***")) +   // identificador: livre (valor digitado por quem doa)
            "6304";                             // CRC (calculado abaixo)
        return payload + Crc16(payload).ToString("X4");
    }

    /// <summary>CRC16-CCITT (polinômio 0x1021, início 0xFFFF), exigido pelo padrão do Pix.</summary>
    static ushort Crc16(string text)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    /// <summary>Imagem do QR code (preto no branco, para qualquer app de banco ler).</summary>
    public static Image CreateQrImage(int pixelsPerModule)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(Payload, QRCodeGenerator.ECCLevel.M);
        byte[] png = new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        using var ms = new MemoryStream(png);
        return new Bitmap(Image.FromStream(ms));
    }
}
