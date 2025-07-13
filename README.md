# DataTable
유니티 데이터테이블

Example.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Example : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        DataTable table = DataTableManager.GetInstance().GetTable(Define.EDataTableType.Item);

        foreach(var dataTableRow in table.Table.Values)
        {
            ItemDataTableRow row = (ItemDataTableRow)dataTableRow;

            string tt = "";
            tt += row.TableID.ToString() + ", ";
            tt += row.Name.ToString() + ", ";
            tt += row.Temp1.ToString() + ", ";
            tt += row.Temp2.ToString() + ", ";
            tt += row.Temp3.ToString() + ", ";

            Debug.Log(tt);
        }

        ItemDataTableRow tRow = DataTableManager.GetInstance().GetTableRow<ItemDataTableRow>(Define.EDataTableType.Item,1);

        string t = "";
        t += tRow.TableID.ToString() + ", ";
        t += tRow.Name.ToString() + ", ";
        t += tRow.Temp1.ToString() + ", ";
        t += tRow.Temp2.ToString() + ", ";
        t += tRow.Temp3.ToString() + ", ";
        Debug.Log(t);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
