#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

// ���� ���� ���� ���� ��ƿ
public static class GoogleSheetsService
{
    // ������ ����Ŭ���� �ܼ��� Ű json ���
    private const string credentialsPath = "Assets/StreamingAssets/credentials.json";

    public static SheetsService GetSheetsService()
    {
        try
        {
            GoogleCredential credential;
            using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);
            }
            return new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Unity Google Sheets Importer"
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"GoogleSheetsService �ʱ�ȭ ����: {e.Message}");
            return null;
        }
    }
}

// CSV �ٿ�ε�� ������ ������ (Sheets API + CSVHelper)
public class GoogleSheetCsvImporter : EditorWindow
{
    private string spreadsheetId = "���۽�Ʈ ���";
    private string outputFolder = "Assets/Data/CSVs";
    private SheetsService service;

    [MenuItem("Tools/DataTable/Google Sheet CSV Importer",priority = 0)]
    public static void ShowWindow()
    {
        GetWindow<GoogleSheetCsvImporter>("Google Sheet CSV Importer");
    }

    private void OnEnable()
    {
        service = GoogleSheetsService.GetSheetsService();
        if (service == null)
            Debug.LogError("Google Sheets Service �ʱ�ȭ ����. StreamingAssets/credentials.json ��� �� ���� Ȯ���ϼ���.");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("���۽�Ʈ ��������", EditorStyles.boldLabel);
        spreadsheetId = EditorGUILayout.TextField("���۽�Ʈ ID", spreadsheetId);
        outputFolder = EditorGUILayout.TextField("CSV���� ���", outputFolder);

        EditorGUILayout.HelpBox("CSV ���� ��Ģ: 1��=�ּ�, 2��=���� Ÿ��, 3��=������", MessageType.Info);

        if (GUILayout.Button("Download All Table Sheets"))
        {
            if (string.IsNullOrEmpty(spreadsheetId))
                Debug.LogError("Spreadsheet ID�� �Է��ϼ���.");
            else if (service == null)
                Debug.LogError("SheetsService�� ��ȿ���� �ʽ��ϴ�.");
            else
                DownloadAllSheetsViaApi();
        }
    }

    private void DownloadAllSheetsViaApi()
    {
        Spreadsheet spreadsheet;
        try
        {
            spreadsheet = service.Spreadsheets.Get(spreadsheetId).Execute();
        }
        catch (Exception e)
        {
            Debug.LogError($"��Ʈ ��Ÿ������ �ε� ����: {e.Message}");
            return;
        }

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        foreach (var sheet in spreadsheet.Sheets)
        {
            var prop = sheet.Properties;
            if (prop.Title.EndsWith("Table"))
            {
                WriteSheetValuesToCsv(prop.Title, prop.Title + ".csv");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("��� ���̺� CSV �ٿ�ε� �Ϸ�.");
    }

    private void WriteSheetValuesToCsv(string sheetName, string fileName)
    {
        string range = $"{sheetName}!A:Z"; // �ʿ信 ���� ���� ����
        ValueRange response;
        try
        {
            response = service.Spreadsheets.Values.Get(spreadsheetId, range).Execute();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"��Ʈ '{sheetName}' �����͸� �������� ����: {e.Message}");
            return;
        }

        var values = response.Values;
        if (values == null || values.Count == 0)
        {
            Debug.LogWarning($"��Ʈ '{sheetName}'�� �����Ͱ� �����ϴ�.");
            return;
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            ShouldQuote = args => true
        };

        string path = Path.Combine(outputFolder, fileName);
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
        Debug.Log($"'{fileName}' CSV ���� �Ϸ�: {path}");
    }
}
#endif
