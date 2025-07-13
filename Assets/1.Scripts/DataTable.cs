using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "DataTable",menuName = "DataTable" )]
public class DataTable : ScriptableObject
{
    [SerializeReference,HideInInspector] // ���� ����� Ŭ���� Ÿ���� �޾ƿ��� ���� SerializeReferance ���, (Unity 2020.1+)
    public List<DataTableRow> serializedRows = new();

    private Dictionary<int, DataTableRow> rows;

    public Dictionary<int, DataTableRow> Table => rows;

    public T GetTableRow<T>(int tableID) where T : DataTableRow
    {
        if (!rows.TryGetValue(tableID, out DataTableRow row))
        {
            Debug.LogWarning($"[DataTable] '{tableID}' ������ ����");
            return null;
        }

        if (row is T castedRow)
            return castedRow;

        Debug.LogError($"[DataTable] '{tableID}' ���̺��� {typeof(T).Name} ���� ĳ��Ʈ ����");
        return null;
    }

    public void Deserialize()
    {
        try
        {
            rows = serializedRows.ToDictionary(r => r.TableID, r => r);
        }
        catch (ArgumentNullException e)
        {
            Debug.LogError($"[DataTable] Deserialize ����: serializedRows ����Ʈ�� ���Դϴ�.\n{e}");
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[DataTable] Deserialize ����: �ߺ��� TableID�� �ֽ��ϴ�.\n" +
                           $"�ߺ� ID Ȯ�� �ʿ� (��: {FindFirstDuplicateID()})\n{e}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // �ߺ��� ID�� ã�� ��ȯ�ϴ� ������ ���� �Լ�
    private int FindFirstDuplicateID()
    {
        var seen = new HashSet<int>();
        foreach (var row in serializedRows)
        {
            if (!seen.Add(row.TableID))
                return row.TableID;
        }
        return -1;
    }
}
