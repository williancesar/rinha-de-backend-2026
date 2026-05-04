// src/Rinha.Api/McCRiskTable.cs
using System.Collections.Frozen;
using System.Text.Json;

namespace Rinha.Api;

/// <summary>
/// Tabela MCC → risco em [0, 1]. Carregada do mcc_risk.json embarcado na imagem.
/// Default 0.5 para MCCs ausentes.
/// </summary>
public sealed class McCRiskTable
{
    private readonly FrozenDictionary<string, float> _table;
    public const float Default = 0.5f;

    public McCRiskTable(FrozenDictionary<string, float> table) => _table = table;

    public float Get(string mcc) => _table.TryGetValue(mcc, out var v) ? v : Default;

    public static McCRiskTable Load(string path)
    {
        using var fs = File.OpenRead(path);
        using var doc = JsonDocument.Parse(fs);
        var dict = new Dictionary<string, float>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.GetSingle();
        }
        return new McCRiskTable(dict.ToFrozenDictionary());
    }
}
