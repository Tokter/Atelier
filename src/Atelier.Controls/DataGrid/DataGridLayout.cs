using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Atelier.Controls;

/// <summary>The saved state of a <see cref="DataGrid"/> column: its place, width and visibility.</summary>
/// <param name="Key">The column's <see cref="DataGridColumn.EffectiveKey"/>.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="IsVisible">Whether the column is shown.</param>
public sealed record DataGridColumnLayout(string Key, float Width, bool IsVisible);

/// <summary>A sort column of a saved <see cref="DataGrid"/> layout.</summary>
/// <param name="Key">The column's <see cref="DataGridColumn.EffectiveKey"/>.</param>
/// <param name="Direction">The sort direction.</param>
public sealed record DataGridSortLayout(string Key, DataGridSortDirection Direction);

/// <summary>
/// The user-adjustable state of a <see cref="DataGrid"/>: column order, widths and visibility, and the sort. Get it with
/// <see cref="DataGrid.SaveLayout"/>, store it (for example with <see cref="ToJson"/>), and apply it later with
/// <see cref="DataGrid.RestoreLayout"/>.
/// </summary>
public sealed class DataGridLayout
{
    /// <summary>Gets the columns in display order.</summary>
    public List<DataGridColumnLayout> Columns { get; } = [];

    /// <summary>Gets the sort columns, the primary one first.</summary>
    public List<DataGridSortLayout> Sort { get; } = [];

    /// <summary>Writes the layout as JSON, e.g. to store in the app's settings.</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("columns");
            foreach (var column in Columns)
            {
                writer.WriteStartObject();
                writer.WriteString("key", column.Key);
                writer.WriteNumber("width", column.Width);
                writer.WriteBoolean("visible", column.IsVisible);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("sort");
            foreach (var sort in Sort)
            {
                writer.WriteStartObject();
                writer.WriteString("key", sort.Key);
                writer.WriteString("direction", sort.Direction.ToString());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Reads a layout written by <see cref="ToJson"/>; unknown or invalid parts are skipped.</summary>
    /// <exception cref="JsonException">The text isn't JSON.</exception>
    public static DataGridLayout FromJson(string json)
    {
        var layout = new DataGridLayout();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("columns", out var columns) && columns.ValueKind == JsonValueKind.Array)
        {
            foreach (var column in columns.EnumerateArray())
            {
                if (column.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String)
                {
                    float width = column.TryGetProperty("width", out var w) && w.TryGetSingle(out float f) ? f : float.NaN;
                    bool visible = !column.TryGetProperty("visible", out var v) || v.ValueKind != JsonValueKind.False;
                    layout.Columns.Add(new DataGridColumnLayout(key.GetString()!, width, visible));
                }
            }
        }

        if (root.TryGetProperty("sort", out var sorts) && sorts.ValueKind == JsonValueKind.Array)
        {
            foreach (var sort in sorts.EnumerateArray())
            {
                if (sort.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
                    && sort.TryGetProperty("direction", out var d) && Enum.TryParse(d.GetString(), out DataGridSortDirection direction))
                {
                    layout.Sort.Add(new DataGridSortLayout(key.GetString()!, direction));
                }
            }
        }
        return layout;
    }
}
