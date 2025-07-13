#if UNITY_EDITOR
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
#endif