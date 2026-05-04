// src/Rinha.IndexBuilder/ReferencesReader.cs
using System.IO.Compression;
using System.Text.Json;
using Rinha.Indexing;

namespace Rinha.IndexBuilder;

/// <summary>
/// Lê references.json.gz em streaming. Formato: array de objetos {vector: [14 floats], label: "fraud"|"legit"}.
/// Output: vectors[N*D] e labels[N] em arrays já dimensionados (alocação única).
/// </summary>
public static class ReferencesReader
{
    public static async Task<(float[] vectors, byte[] labels, int n)> ReadAsync(string gzPath, CancellationToken ct = default)
    {
        // Sondagem inicial para descobrir N. references.json.gz oficial tem N=3_000_000;
        // mas em testes/uso futuro com gzs menores, evitamos hardcode.
        // Leitura única: contamos durante o parse e cresce-mos arrays via List<>.
        var vectorsList = new List<float>(capacity: 3_000_000 * IndexFileLayout.VectorDim);
        var labelsList = new List<byte>(capacity: 3_000_000);

        await using var fs = File.OpenRead(gzPath);
        await using var gz = new GZipStream(fs, CompressionMode.Decompress);
        var bufferSize = 1 << 20; // 1 MB
        await using var bs = new BufferedStream(gz, bufferSize);

        var doc = await JsonDocument.ParseAsync(bs, cancellationToken: ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("references must be a top-level JSON array");

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var vec = entry.GetProperty("vector");
            if (vec.GetArrayLength() != IndexFileLayout.VectorDim)
                throw new InvalidDataException($"vector dim mismatch: expected {IndexFileLayout.VectorDim}");
            foreach (var f in vec.EnumerateArray())
                vectorsList.Add(f.GetSingle());

            var label = entry.GetProperty("label").GetString();
            labelsList.Add(label switch
            {
                "fraud" => IndexFileLayout.LabelFraud,
                "legit" => IndexFileLayout.LabelLegit,
                _ => throw new InvalidDataException($"unknown label: {label}")
            });
        }

        return (vectorsList.ToArray(), labelsList.ToArray(), labelsList.Count);
    }
}
