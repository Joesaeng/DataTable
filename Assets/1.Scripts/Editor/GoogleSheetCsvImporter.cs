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

// 구글 서비스 계정 인증 유틸
public static class GoogleSheetsService
{
    // 생성한 구글클라우드 콘솔의 키 json 경로
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
            Debug.LogError($"GoogleSheetsService 초기화 실패: {e.Message}");
            return null;
        }
    }
}

// CSV 다운로드용 에디터 윈도우 (Sheets API + CSVHelper)
public class GoogleSheetCsvImporter : EditorWindow
{
    private string spreadsheetId = "구글시트 경로";
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
            Debug.LogError("Google Sheets Service 초기화 실패. StreamingAssets/credentials.json 경로 및 내용 확인하세요.");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("구글시트 가져오기", EditorStyles.boldLabel);
        spreadsheetId = EditorGUILayout.TextField("구글시트 ID", spreadsheetId);
        outputFolder = EditorGUILayout.TextField("CSV생성 경로", outputFolder);

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
            Debug.LogError($"시트 메타데이터 로드 실패: {e.Message}");
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
        Debug.Log($"'{fileName}' CSV 생성 완료: {path}");
    }
}
#endif
