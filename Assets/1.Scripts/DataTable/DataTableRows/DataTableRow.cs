using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DataTableRow
{
    [SerializeField] private int _tableID;

    public int TableID => _tableID;
}
