using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "DataTable",menuName = "DataTable" )]
public class DataTable : ScriptableObject
{
    [SerializeReference,HideInInspector] // 실제 사용할 클래스 타입을 받아오기 위해 SerializeReferance 사용, (Unity 2020.1+)
    public List<DataTableRow> serializedRows = new();

    private Dictionary<int, DataTableRow> rows;

    public Dictionary<int, DataTableRow> Table => rows;

    public T GetTableRow<T>(int tableID) where T : DataTableRow
    {
        if (!rows.TryGetValue(tableID, out DataTableRow row))
        {
            Debug.LogWarning($"[DataTable] '{tableID}' 데이터 없음");
            return null;
        }

        if (row is T castedRow)
            return castedRow;

        Debug.LogError($"[DataTable] '{tableID}' 테이블을 {typeof(T).Name} 으로 캐스트 실패");
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
            Debug.LogError($"[DataTable] Deserialize 실패: serializedRows 리스트가 널입니다.\n{e}");
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[DataTable] Deserialize 실패: 중복된 TableID가 있습니다.\n" +
                           $"중복 ID 확인 필요 (예: {FindFirstDuplicateID()})\n{e}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // 중복된 ID를 찾아 반환하는 간단한 헬퍼 함수
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
