#if UNITY_EDITOR
using CsvHelper.Configuration;
using CsvHelper;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor.PackageManager.UI;

public class DataTableToolkitWindow : EditorWindow
{
    // GoogleSheetCsvImporter
    private string spreadsheetId = "구글시트 경로";
    private string csvOutputFolder = "Assets/Data/CSVs";
    private SheetsService service;

    // DataTableRowGenerator
    private TextAsset dataTalbeRowCsvAsset;
    private string inputCsvFolder = "Assets/Data/CSVs"; // 프로젝트 내 CSV 파일 폴더 경로
    private string dataTalbwRowOutputFolder = "Assets/1.Scripts/DataTable/DataTableRows"; // 생성할 DataTableRow 클래스 파일 경로
    private string namespaceName = string.Empty;

    // DataTableSOGeneretor
    private TextAsset dataTableCsvAsset;
    private string soOutputFolder  = "Assets/Resources/DataTables"; // 스크립터블 오브젝트를 생성할 폴더 경로
    private string dataTableDefinePath = "Assets/1.Scripts/DataTable"; // DataTable Define 파일을 생성할 폴더 경로

    [MenuItem("Tools/DataTable/DataTableToolkit", priority = 0)]
    public static void ShowSOWindow()
    {
        var window = GetWindow<DataTableToolkitWindow>("데이터테이블 툴킷");
        window.minSize = new Vector2(400, 650);
        window.Show();
    }

    private void OnEnable()
    {
        service = GoogleSheetsService.GetSheetsService();
        if (service == null)
            Debug.LogError("Google Sheets Service 초기화 실패. StreamingAssets/credentials.json 경로 및 내용 확인하세요.");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("구글시트 가져오기", EditorStyles.boldLabel);
        spreadsheetId = EditorGUILayout.TextField("구글시트 ID", spreadsheetId);
        csvOutputFolder = EditorGUILayout.TextField("CSV생성 경로", csvOutputFolder);

        EditorGUILayout.HelpBox("CSV 파일 규칙: 1행=주석, 2행=변수 타입, 3행=변수명", MessageType.Info);

        if (GUILayout.Button("Download All Table Sheets"))
        {
            if (string.IsNullOrEmpty(spreadsheetId))
                Debug.LogError("Spreadsheet ID를 입력하세요.");
            else if (service == null)
                Debug.LogError("SheetsService가 유효하지 않습니다.");
            else
                DownloadAllSheetsViaApi();
        }

        GUILayout.Space(25);
        EditorGUILayout.LabelField("CSV → DataTableRow 클래스 생성기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("CSV 파일 규칙: 1행=주석, 2행=변수 타입, 3행=변수명", MessageType.Info);

        dataTalbeRowCsvAsset = (TextAsset)EditorGUILayout.ObjectField("CSV File", dataTalbeRowCsvAsset, typeof(TextAsset), false);
        inputCsvFolder = EditorGUILayout.TextField("Input CSV Folder", inputCsvFolder);
        dataTalbwRowOutputFolder = EditorGUILayout.TextField("Output Folder", dataTalbwRowOutputFolder);
        namespaceName = EditorGUILayout.TextField("Namespace (옵션)", namespaceName);

        // CSV 에셋이 없으면 비활성화된 버튼 표시
        GUILayout.Space(10);
        EditorGUI.BeginDisabledGroup(dataTalbeRowCsvAsset == null);
        if (GUILayout.Button("Generate Row Class"))
        {
            GenerateRowClass(dataTalbeRowCsvAsset);
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        if (GUILayout.Button("Generate All from Folder"))
        {
            GenerateAllRowClasses();
        }

        GUILayout.Space(25);
        EditorGUILayout.LabelField("CSV → DataTable SO 생성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "CSV 폴더의 .csv 파일을 읽어 DataTable SO 에셋을 생성합니다.\n" +
            "1행=주석, 2행=타입, 3행=변수명, 4행부터 데이터", MessageType.Info);

        dataTableCsvAsset = (TextAsset)EditorGUILayout.ObjectField("CSV File", dataTableCsvAsset, typeof(TextAsset), false);
        inputCsvFolder = EditorGUILayout.TextField("CSV 파일 경로", inputCsvFolder);
        soOutputFolder = EditorGUILayout.TextField("데이터테이블 SO 생성 경로", soOutputFolder);
        dataTableDefinePath = EditorGUILayout.TextField("DataTableDefine 생성 경로", dataTableDefinePath);
        namespaceName = EditorGUILayout.TextField("Namespace (옵션)", namespaceName);

        // CSV 에셋이 없으면 비활성화된 버튼 표시
        GUILayout.Space(10);
        EditorGUI.BeginDisabledGroup(dataTalbeRowCsvAsset == null);
        if (GUILayout.Button("선택한 SO 에셋 생성"))
        {
            GenerateSOAsset(dataTalbeRowCsvAsset);
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        if (GUILayout.Button("모든 SO 에셋 생성"))
            GenerateAllSOAssets();

        GUILayout.Space(10);
        if (GUILayout.Button("DataTableDefine 생성"))
            GenerateDataTableDefine();
    }

    #region GoogleSheetsCsvImporter
    private void DownloadAllSheetsViaApi()
    {
        Spreadsheet spreadsheet;
        try
        {
            spreadsheet = service.Spreadsheets.Get(spreadsheetId).Execute();
        }
        catch (Exception e)
        {
            Debug.LogError($"시트 메타데이터 로드 실패: {e.Message}");
            return;
        }

        if (!Directory.Exists(csvOutputFolder))
            Directory.CreateDirectory(csvOutputFolder);

        foreach (var sheet in spreadsheet.Sheets)
        {
            var prop = sheet.Properties;
            if (prop.Title.EndsWith("Table"))
            {
                WriteSheetValuesToCsv(prop.Title, prop.Title + ".csv");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("모든 테이블 CSV 다운로드 완료.");
    }

    private void WriteSheetValuesToCsv(string sheetName, string fileName)
    {
        string range = $"{sheetName}!A:Z"; // 필요에 따라 범위 조정
        ValueRange response;
        try
        {
            response = service.Spreadsheets.Values.Get(spreadsheetId, range).Execute();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"시트 '{sheetName}' 데이터를 가져오기 실패: {e.Message}");
            return;
        }

        var values = response.Values;
        if (values == null || values.Count == 0)
        {
            Debug.LogWarning($"시트 '{sheetName}'에 데이터가 없습니다.");
            return;
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            ShouldQuote = args => true
        };

        string path = Path.Combine(csvOutputFolder, fileName);
        using (var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(true)))
        using (var csv = new CsvWriter(writer, config))
        {
            foreach (var row in values)
            {
                foreach (var cell in row)
                {
                    csv.WriteField(cell?.ToString() ?? string.Empty);
                }
                csv.NextRecord();
            }
        }
        Debug.Log($"'{fileName}' CSV 생성 완료: {path}");
    }
    #endregion

    #region DataTableRowGenerator
    private void GenerateAllRowClasses()
    {
        if (!Directory.Exists(inputCsvFolder))
        {
            Debug.LogError($"CSV 폴더를 찾을 수 없습니다: {inputCsvFolder}");
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
        dataTalbeRowCsvAsset = null;
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
        string filePath  = Path.Combine(dataTalbwRowOutputFolder, className + ".cs");

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

        if (!Directory.Exists(dataTalbwRowOutputFolder))
            Directory.CreateDirectory(dataTalbwRowOutputFolder);
        File.WriteAllText(filePath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[DataTableRow 생성기] {className} 생성 완료: {asset.name}");
    }
    #endregion

    #region DataTableSOGenerator
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

        if (!Directory.Exists(outputFolder))
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
    #endregion
}
#endif