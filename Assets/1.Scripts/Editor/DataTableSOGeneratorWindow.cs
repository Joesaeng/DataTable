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
    private string inputCsvFolder = "Assets/Data/CSVs"; // 프로젝트 내 CSV 파일 경로
    private string soOutputFolder  = "Assets/Resources/DataTables"; // 스크립터블 오브젝트를 생성할 폴더 경로
    private string dataTableDefinePath = "Assets/1.Scripts/DataTable"; // DataTable Define 파일을 생성할 폴더 경로
    private string namespaceName   = string.Empty;

    [MenuItem("Tools/DataTable/Generate SO Assets", priority = 2)]
    public static void ShowSOWindow() =>
        GetWindow<DataTableSOGeneratorWindow>("SO Asset Generator");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("CSV → DataTable SO 생성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "CSV 폴더의 .csv 파일을 읽어 DataTable SO 에셋을 생성합니다.\n" +
            "1행=주석, 2행=타입, 3행=변수명, 4행부터 데이터", MessageType.Info);

        csvAsset = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvAsset, typeof(TextAsset), false);
        inputCsvFolder = EditorGUILayout.TextField("CSV 파일 경로", inputCsvFolder);
        soOutputFolder = EditorGUILayout.TextField("데이터테이블 SO 생성 경로", soOutputFolder);
        dataTableDefinePath = EditorGUILayout.TextField("DataTableDefine 생성 경로", dataTableDefinePath);
        namespaceName = EditorGUILayout.TextField("Namespace (옵션)", namespaceName);

        // CSV 에셋이 없으면 비활성화된 버튼 표시
        GUILayout.Space(10);
        EditorGUI.BeginDisabledGroup(csvAsset == null);
        if (GUILayout.Button("선택한 SO 에셋 생성"))
        {
            GenerateSOAsset(csvAsset);
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        if (GUILayout.Button("모든 SO 에셋 생성"))
            GenerateAllSOAssets();

        GUILayout.Space(10);
        if (GUILayout.Button("DataTableDefine 생성"))
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
            Debug.LogError($"CSV 폴더를 찾을 수 없습니다: {inputCsvFolder}");
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
        Debug.Log($"[DataTable 생성기] {fileName} 생성 완료");
    }

    private void CreateSO(TextAsset ta)
    {
        var lines = ta.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 4)
        {
            Debug.LogError($"CSV 데이터가 부족합니다: {ta.name}");
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
            Debug.LogError($"Row 타입을 찾을 수 없습니다: {rowTypeName}");
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
        Debug.Log($"DataTable SO 생성 완료: {assetName}");
    }
}
#endif
