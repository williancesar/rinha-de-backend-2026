// src/Rinha.Api/JsonContext.cs
using System.Text.Json.Serialization;

namespace Rinha.Api;

public sealed record FraudRequest(
    string Id,
    Transaction Transaction,
    Customer Customer,
    Merchant Merchant,
    Terminal Terminal,
    LastTransaction? LastTransaction);

public sealed record Transaction(double Amount, int Installments, string RequestedAt);

public sealed record Customer(double AvgAmount, int TxCount24h, string[] KnownMerchants);

public sealed record Merchant(string Id, string Mcc, double AvgAmount);

public sealed record Terminal(bool IsOnline, bool CardPresent, double KmFromHome);

public sealed record LastTransaction(string Timestamp, double KmFromCurrent);

public sealed record FraudResponse(bool Approved, float FraudScore);

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(FraudRequest))]
[JsonSerializable(typeof(FraudResponse))]
public partial class JsonContext : JsonSerializerContext { }
