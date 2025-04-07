using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Austin.SqlDataLineageAnalyzer;

namespace Austin
{
    public class TempTableAnalyzer : TSqlFragmentVisitor
    {
        private readonly Dictionary<string, TempTableInfo> _tableRegistry = new();
        private TempTableInfo? _currentTable;

        public override void Visit(CreateTableStatement node)
        {
            if (node.SchemaObjectName is not SchemaObjectName schemaObject)
                return;

            string rawTableName = GetTableName(schemaObject);

            if (IsTempTable(rawTableName))
            {
                _currentTable = new TempTableInfo
                {
                    Name = NormalizeTempTableName(rawTableName),
                    Type = TableType.Temp,
                    SourceType = TempTableSourceType.CreateTable,
                    Columns = GetColumns(node)
                };

                _tableRegistry[_currentTable.Name] = _currentTable;
            }
        }

        private string GetTableName(SchemaObjectName schemaObject)
        {
            return schemaObject.Identifiers
                .LastOrDefault()?.Value
                ?? string.Empty;
        }

        private string NormalizeTempTableName(string name)
        {
            if (name.StartsWith("tempdb.."))
                return name.Replace("tempdb..", "#");

            if (name.Contains(".#"))
                return name.Substring(name.LastIndexOf(".#") + 1);

            return name.StartsWith("#") ? name : $"#{name}";
        }

        private bool IsTempTable(string name) =>
            name.StartsWith("#") || name.Contains("tempdb..#");

        private List<ColumnInfo> GetColumns(CreateTableStatement node)
        {
            var columns = new List<ColumnInfo>();

            // 处理显式列定义
            if (node.Definition.ColumnDefinitions != null)
            {
                columns.AddRange(node.Definition.ColumnDefinitions.Select(cd => new ColumnInfo
                {
                    OriginalName = GetColumnName(cd),
                    DataType = GetDataType(cd.DataType),
                }));
            }

            // 处理CTAS隐式列（来自SELECT子句）
            if (node.SelectStatement?.QueryExpression is QuerySpecification select)
            {
                var selectColumns = select.SelectElements.OfType<SelectScalarExpression>();

                columns.AddRange(selectColumns.Select(s => new ColumnInfo
                {
                    OriginalName = GetIdentifierName(s.ColumnName),
                    DataType = InferDataTypeFromExpression(s.Expression),
                }));
            }

            return columns;
        }

        private string GetDataType(DataTypeReference dataType)
        {
            if (dataType.Name is not null)
                return dataType.Name.Identifiers.LastOrDefault()?.Value ?? "UNKNOWN";

            return "UNKNOWN";
        }

        private string GetIdentifierName(IdentifierOrValueExpression columnName)
        {
            return columnName?.Value ?? string.Empty;
        }

        private string GetColumnName(ColumnDefinition cd)
        {
            return cd.ColumnIdentifier?.Value
                   ?? cd.ComputedColumnExpression?.ToString()
                   ?? string.Empty;
        }

        private string GetIdentifierName(Identifier identifier)
        {
            return identifier?.Value ?? string.Empty;
        }

        private string GetDataType(SqlDataTypeReference dataType)
        {
            if (dataType.Name is not null)
                return dataType.Name.Identifiers.LastOrDefault()?.Value ?? "UNKNOWN";

            return "UNKNOWN";
        }

        private string InferDataTypeFromExpression(ScalarExpression expression)
        {
            return expression switch
            {
                Literal literal => GetLiteralDataType(literal),
                ColumnReferenceExpression col => col.MultiPartIdentifier.Identifiers.Last().Value,
                FunctionCall func => func.FunctionName.Value ?? "UNKNOWN",
                _ => "UNKNOWN"
            };
        }

        private string GetLiteralDataType(Literal literal)
        {
            return literal switch
            {
                NumericLiteral => "NUMERIC",
                StringLiteral => "VARCHAR",
                _ => "UNKNOWN"
            };
        }
    }

}
