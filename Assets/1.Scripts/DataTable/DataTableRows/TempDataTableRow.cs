using System;
using UnityEngine;
[System.Serializable]
public class TempDataTableRow : DataTableRow
{
    [SerializeField] private string _name;
    [SerializeField] private uint _temp1;
    [SerializeField] private string _temp2;
    [SerializeField] private float _temp3;
    [SerializeField] private long _temp4;

    public string Name => _name;
    public uint Temp1 => _temp1;
    public string Temp2 => _temp2;
    public float Temp3 => _temp3;
    public long Temp4 => _temp4;
}
