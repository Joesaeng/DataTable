using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Define;
using System;
using System.Linq;

public class DataTableManager : MonoBehaviour
{
    private static DataTableManager instance;

    public static DataTableManager GetInstance() { return instance; }

    private Dictionary<EDataTableType, DataTable> dataTableDict;

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);

        dataTableDict = new();

        for(int i = (int)EDataTableType.None + 1; i < (int)EDataTableType.Max;++i)
        {
            var type = (EDataTableType)i;

            var table = Resources.Load<DataTable>($"DataTables/{type}DataTable");
            if (table != null)
                dataTableDict[type] = table;
            else
                Debug.LogWarning($"[DataTableManager] '{type}DataTable' 없음");
        }

        foreach(var dataTable in dataTableDict.Values)
        {
            dataTable.Deserialize();
        }
    }

    public DataTable GetTable(EDataTableType type) 
    {
        if (!dataTableDict.TryGetValue(type, out var table))
        {
            Debug.LogWarning($"[DataTableManager] '{type}' 테이블 없음");
            return null;
        }

        return table;
    }

    public T GetTableRow<T>(EDataTableType type, int tableID) where T : DataTableRow
    {
        if(!dataTableDict.TryGetValue(type,out var dataTable))
        {
            Debug.LogWarning($"[DataTableManager] '{type}' 테이블 없음");
            return null;
        }

        return dataTable.GetTableRow<T>(tableID);
    }
}
