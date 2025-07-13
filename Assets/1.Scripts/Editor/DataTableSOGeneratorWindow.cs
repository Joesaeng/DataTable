#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.VersionControl;
using UnityEngine;

public class DataTableSOGeneratorWindow : EditorWindow
{
    private TextAsset csvAsset;
    private string inputCsvFolder = "Assets/Data/CSVs"; // ������Ʈ �� CSV ���� ���
    private string soOutputFolder  = "Assets/Resources/DataTables"; // ��ũ���ͺ� ������Ʈ�� ������ ���� ���
    private string dataTableDefinePath = "Assets/1.Scripts/DataTable"; // DataTable Define ������ ������ ���� ���
    private string namespaceName   = string.Empty;

    [MenuItem("Tools/DataTable/Generate SO Assets", priority = 2)]
    public static void ShowSOWindow() =>
        GetWindow<DataTableSOGeneratorWindow>("SO Asset Generator");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("CSV �� DataTable SO ����", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "CSV ������ .csv ������ �о� DataTable SO ������ �����մϴ�.\n" +
            "1��=�ּ�, 2��=Ÿ��, 3��=������, 4����� ������", MessageType.Info);

        csvAsset = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvAsset, typeof(TextAsset), false);
        inputCsvFolder = EditorGUILayout.TextField("CSV ���� ���", inputCsvFolder);
        soOutputFolder = EditorGUILayout.TextField("���������̺� SO ���� ���", soOutputFolder);
        dataTableDefinePath = EditorGUILayout.TextField("DataTableDefine ���� ���", dataTableDefinePath);
        namespaceName = EditorGUILayout.TextField("Namespace (�ɼ�)", namespaceName);

        // CSV ������ ������ ��Ȱ��ȭ�� ��ư ǥ��
        GUILayout.Space(10);
        EditorGUI.BeginDisabledGroup(csvAsset == null);
        if (GUILayout.Button("������ SO ���� ����"))
        {
            GenerateSOAsset(csvAsset);
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        if (GUILayout.Button("��� SO ���� ����"))
            GenerateAllSOAssets();

        GUILayout.Space(10);
        if (GUILayout.Button("DataTableDefine ����"))
            GenerateDataTableDefine();
    }

    private void GenerateSOAsset(TextAsset asset)
    {
        CreateSO(asset);
    }

    private void GenerateAllSOAssets()
    {
        if (!Directory.Exists(inputCsvFolder))
        {
            Debug.LogError($"CSV ������ ã�� �� �����ϴ�: {inputCsvFolder}");
            return;
        }

        var csvFiles = Directory.GetFiles(inputCsvFolder, "*.csv", SearchOption.TopDirectoryOnly);
        foreach (var path in csvFiles)
        {
            var assetPath = path.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (ta != null)
                CreateSO(ta);
        }
    }

    private void GenerateDataTableDefine()
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Define");
        sb.AppendLine("{");
        sb.AppendLine("     public enum EDataTableType");
        sb.AppendLine("     {");
        sb.AppendLine("         None = 0,");

        var csvFiles = Directory.GetFiles(inputCsvFolder, "*.csv", SearchOption.TopDirectoryOnly);
        foreach (var path in csvFiles)
        {
            var space = "         ";
            var assetPath = path.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (ta != null)
            {
                var typeName = space + ta.name.Replace("DataTable", "") + ",";
                sb.AppendLine($"{typeName}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("         Max,");
        sb.AppendLine("     }");
        sb.AppendLine("}");

        string fileName = "DataTableDefine";
        string outputFolder = dataTableDefinePath;

        if(!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        string filePath  = Path.Combine(outputFolder, fileName + ".cs");

        File.WriteAllText(filePath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[DataTable ������] {fileName} ���� �Ϸ�");
    }

    private void CreateSO(TextAsset ta)
    {
        var lines = ta.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 4)
        {
            Debug.LogError($"CSV �����Ͱ� �����մϴ�: {ta.name}");
            return;
        }

        var headers = lines[2].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
        var rowsData = lines.Skip(3)
            .Select(l => l.Split(',').Select(c => c.Trim().Trim('"')).ToArray())
            .ToList();

        var baseName  = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(ta));
        var assetName = baseName + ".asset";
        var assetPath = Path.Combine(soOutputFolder, assetName);

        // Create SO
        var so = ScriptableObject.CreateInstance<DataTable>();
        so.serializedRows.Clear();

        // Find row type by class name
        var rowTypeName = baseName + "Row";
        // First try the assembly where DataTableRow is defined
        var runtimeAsm = typeof(DataTableRow).Assembly;
        var rowType = runtimeAsm.GetType(rowTypeName);
        // Fallback: search all loaded assemblies
        if (rowType == null)
            rowType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(rowTypeName))
                .FirstOrDefault(t => t != null);

        if (rowType == null)
        {
            Debug.LogError($"Row Ÿ���� ã�� �� �����ϴ�: {rowTypeName}");
            return;
        }

        // Populate SO
        foreach (var line in rowsData)
        {
            // Ensure each row has same length as headers
            var cells = new string[headers.Length];
            for (int i = 0; i < headers.Length; i++)
                cells[i] = i < line.Length ? line[i] : string.Empty;

            // Create row instance
            var rowInstance = (DataTableRow)Activator.CreateInstance(rowType);
            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i];
                var fieldName = "_" + char.ToLowerInvariant(header[0]) + header.Substring(1);
                var fi = rowType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

                if (header.Equals("TableID", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(cells[i], out var id))
                        typeof(DataTableRow)
                            .GetField("_tableID", BindingFlags.NonPublic | BindingFlags.Instance)
                            .SetValue(rowInstance, id);
                    else
                        Debug.LogWarning($"Invalid TableID in {ta.name}: '{cells[i]}'");
                }
                else if (fi != null)
                {
                    if (string.IsNullOrEmpty(cells[i]))
                    {
                        // default value
                        var defaultVal = fi.FieldType.IsValueType ? Activator.CreateInstance(fi.FieldType) : null;
                        fi.SetValue(rowInstance, defaultVal);
                    }
                    else
                    {
                        var converted = Convert.ChangeType(cells[i], fi.FieldType);
                        fi.SetValue(rowInstance, converted);
                    }
                }
            }
            so.serializedRows.Add(rowInstance);
        }

        // Save asset
        Directory.CreateDirectory(soOutputFolder);
        AssetDatabase.CreateAsset(so, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"DataTable SO ���� �Ϸ�: {assetName}");
    }
}
#endif
