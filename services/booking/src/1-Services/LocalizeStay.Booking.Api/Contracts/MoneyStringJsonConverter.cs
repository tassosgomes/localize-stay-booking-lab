using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalizeStay.Booking.Api.Contracts;

// Serializa decimal monetário como string decimal de 2 casas ("350.00"),
// nunca número de ponto flutuante — premissa do api-contract.yaml (PD-001).
// Aplicado apenas nas propriedades do ReservationResponseDto; o Domain segue
// com decimal puro.
public sealed class MoneyStringJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        if (reader.TokenType == JsonTokenType.String &&
            decimal.TryParse(
                reader.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new JsonException(
            $"Valor monetário inválido: esperado string decimal com 2 casas, encontrado " +
            $"'{reader.GetString() ?? reader.TokenType.ToString()}'.");
    }

    public override void Write(
        Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("F2", CultureInfo.InvariantCulture));
    }
}
