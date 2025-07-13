#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text;
using System;

public class DataTableRowGeneratorWindow : EditorWindow
{
    private TextAsset csvAsset;
    private string inputCsvFolder = "Assets/Data/CSVs"; // ������Ʈ �� CSV ���� ���� ���
    private string outputFolder = "Assets/1.Scripts/DataTable/DataTableRows"; // ������ DataTableRow Ŭ���� ���� ���
    private string namespaceName = string.Empty;

    [MenuItem("Tools/DataTable/DataTableRow ������", priority = 1)]
    public static void OpenWindow()
    {
        GetWindow<DataTableRowGeneratorWindow>("DataTableRow ������").Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("CSV �� DataTableRow Ŭ���� ������", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("CSV ���� ��Ģ: 1��=�ּ�, 2��=���� Ÿ��, 3��=������", MessageType.Info);

        csvAsset = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvAsset, typeof(TextAsset), false);
        inputCsvFolder = EditorGUILayout.TextField("Input CSV Folder", inputCsvFolder);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        namespaceName = EditorGUILayout.TextField("Namespace (�ɼ�)", namespaceName);

        // CSV ������ ������ ��Ȱ��ȭ�� ��ư ǥ��
        GUILayout.Space(10);
        EditorGUI.BeginDisabledGroup(csvAsset == null);
        if (GUILayout.Button("Generate Row Class"))
        {
            GenerateRowClass(csvAsset);
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        if (GUILayout.Button("Generate All from Folder"))
        {
            GenerateAllRowClasses();
        }
    }

    private void GenerateAllRowClasses()
    {
        if (!Directory.Exists(inputCsvFolder))
        {
            Debug.LogError($"CSV ������ ã�� �� �����ϴ�: {inputCsvFolder}");
            return;
        }
        var files = Directory.GetFiles(inputCsvFolder, "*.csv", SearchOption.TopDirectoryOnly);
        foreach (var path in files)
        {
            var assetPath = path.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (textAsset == null)
            {
                Debug.LogWarning($"Not a TextAsset: {assetPath}");
                continue;
            }
            GenerateRowClass(textAsset);
        }
        csvAsset = null;
    }

    private void GenerateRowClass(TextAsset asset)
    {
        var lines = asset.text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 3)
        {
            Debug.LogError($"CSV must have at least 3 rows: {asset.name}");
            return;
        }
        var typeTokens = lines[1].Split(',').Select(t => t.Trim().Trim('"')).ToArray();
        var nameTokens = lines[2].Split(',').Select(t => t.Trim().Trim('"')).ToArray();
        if (typeTokens.Length != nameTokens.Length)
        {
            Debug.LogError($"Count mismatch in {asset.name}");
            return;
        }
        string baseName  = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(asset));
        string className = $"{baseName}Row";
        string filePath  = Path.Combine(outputFolder, className + ".cs");

        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using UnityEngine;");
        if (!string.IsNullOrEmpty(namespaceName))
        {
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
        }
        sb.AppendLine("[System.Serializable]");
        sb.AppendLine($"public class {className} : DataTableRow");
        sb.AppendLine("{");
        for (int i = 0; i < nameTokens.Length; i++)
        {
            var header = nameTokens[i];
            var typeName = typeTokens[i];
            if (header.Equals("TableID", StringComparison.OrdinalIgnoreCase))
                continue;
            var field = "_" + char.ToLowerInvariant(header[0]) + header.Substring(1);
            sb.AppendLine($"    [SerializeField] private {typeName} {field};");
        }
        sb.AppendLine();
        for (int i = 0; i < nameTokens.Length; i++)
        {
            var header = nameTokens[i];
            var typeName = typeTokens[i];
            if (header.Equals("TableID", StringComparison.OrdinalIgnoreCase))
                continue;
            var field = "_" + char.ToLowerInvariant(header[0]) + header.Substring(1);
            sb.AppendLine($"    public {typeName} {header} => {field};");
        }
        sb.AppendLine("}");
        if (!string.IsNullOrEmpty(namespaceName))
            sb.AppendLine("}");

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);
        File.WriteAllText(filePath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[DataTableRow ������] {className} ���� �Ϸ�: {asset.name}");
    }
}
#endif
