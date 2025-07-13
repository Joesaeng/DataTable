#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditorInternal;

[CustomEditor(typeof(DataTable))]
public class DataTableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(10);
        if (GUILayout.Button("Open Table Preview"))
        {
            var table = (DataTable)target;
            table.Deserialize();
            DataTableWindow.ShowWindow(table);
        }
    }
}

public class DataTableWindow : EditorWindow
{
    private DataTable table;
    private Vector2 scrollPos;

    public static void ShowWindow(DataTable table)
    {
        var window = GetWindow<DataTableWindow>("DataTable Preview");
        window.table = table;
        window.minSize = new Vector2(400, 200);
        window.Show();
    }

    private void OnGUI()
    {
        if (table == null)
        {
            EditorGUILayout.LabelField("No DataTable loaded.");
            return;
        }

        var rows = table.serializedRows;
        if (rows == null || rows.Count == 0)
        {
            EditorGUILayout.LabelField("No data rows in this table.");
            return;
        }

        // Get columns from first row's public properties
        var rowType = rows[0].GetType();
        var props = rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                           .Where(p => p.CanRead)
                           .ToList();
        // Move TableID to front if exists
        var tableIdProp = props.FirstOrDefault(p => string.Equals(p.Name, "TableID", StringComparison.OrdinalIgnoreCase));
        if (tableIdProp != null)
        {
            props.Remove(tableIdProp);
            props.Insert(0, tableIdProp);
        }
        var orderedProps = props.ToArray();

        // Calculate column width
        float totalWidth = position.width - 25; // account for scroll bar
        int colCount = props.Count;
        float colWidth = Mathf.Max(60, totalWidth / colCount);

        // Header with box style
        EditorGUILayout.BeginHorizontal();
        foreach (var prop in props)
        {
            EditorGUILayout.LabelField(prop.Name, GUI.skin.box, GUILayout.Width(colWidth));
        }
        EditorGUILayout.EndHorizontal();

        // Rows
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (var row in rows)
        {
            EditorGUILayout.BeginHorizontal();
            foreach (var prop in props)
            {
                var value = prop.GetValue(row)?.ToString() ?? string.Empty;
                EditorGUILayout.LabelField(value, GUI.skin.box, GUILayout.Width(colWidth));
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}
#endif
