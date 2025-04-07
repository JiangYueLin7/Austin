using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Austin.SqlDataLineageAnalyzer;

namespace Austin
{
    public class CommonHelper
    {
        private TableInfo ResolveTable(SchemaObjectName name)
        {
            var tableName = name.Identifiers.Last().Value;
            return _tableRegistry.ContainsKey(tableName)
                ? _tableRegistry[tableName]
                : new TableInfo { Name = tableName };
        }

        // 修正属性名：ColumnDefinitions → Columns
        private List<ColumnInfo> GetColumns(IList<ColumnDefinition> columns)
        {
            return columns.Select(cd => new ColumnInfo
            {
                OriginalName = GetColumnName(cd),
                DataType = GetDataType(cd.DataType),
                ParentTable = _queryStack.Peek().CurrentTable
            }).ToList();
        }


        // 正确获取列名（处理别名和计算列）
        private string GetColumnName(ColumnDefinition cd)
        {
            // 优先处理列别名（带AS的情况）
            if (cd.ColumnName != null && cd.ColumnName.Identifier != null)
            {
                return cd.ColumnName.Identifier.Value;
            }

            // 处理计算列（如：Column AS 1+2）
            if (cd.ComputeColumnExpression != null)
            {
                return cd.ComputeColumnExpression.ToString();
            }

            return string.Empty;
        }

        private string GetDataType(DataTypeReference dataType)
        {
            if (dataType == null) return "UNKNOWN";

            // 处理系统类型（如：INT, NVARCHAR）
            if (dataType.BaseTypeName != null)
            {
                return dataType.BaseTypeName.Identifier?.Value
                       ?? dataType.UserDefinedTypeName?.Name
                       ?? "UNKNOWN";
            }

            // 处理用户自定义类型
            if (dataType.UserDefinedTypeName != null)
            {
                return dataType.UserDefinedTypeName.Name;
            }

            return "UNKNOWN";
        }
        // 数据类型解析增强
        private string GetDataType(SqlDataTypeReference dataType)
        {
            if (dataType == null) return "UNKNOWN";

            // 处理系统类型（如：INT, NVARCHAR）
            if (dataType.BaseTypeName != null)
            {
                return dataType.BaseTypeName.Identifier?.Value
                       ?? dataType.UserDefinedTypeName?.Name
                       ?? "UNKNOWN";
            }

            // 处理用户自定义类型
            if (dataType.UserDefinedTypeName != null)
            {
                return dataType.UserDefinedTypeName.Name;
            }

            return "UNKNOWN";
        }
    }
}
