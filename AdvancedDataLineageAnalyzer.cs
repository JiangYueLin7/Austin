using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace AdvancedDataLineageAnalyzer
{
    /// <summary>
    /// Visitor class for analyzing table lineage in T-SQL scripts.
    /// </summary>
    public class TableLineageVisitor : TSqlFragmentVisitor
    {
        public string? CurrentProcedure { get; set; }

        public readonly List<TableReference> Tables = new();
        public readonly List<TempTableInfo> TempTables = new();

        public override void Visit(NamedTableReference node)
        {
            var tableName = GetFullTableName(node.SchemaObject);
            if (!string.IsNullOrEmpty(tableName))
            {
                Tables.Add(new TableReference
                {
                    TableName = tableName,
                    IsTempTable = tableName.StartsWith("#"),
                    ReferenceType = "SELECT",
                    ProcedureName = CurrentProcedure ?? string.Empty
                });
            }
            base.Visit(node);
        }

        public override void Visit(InsertStatement node)
        {
            if (node.InsertSpecification.Target is NamedTableReference targetTable)
            {
                var tableName = GetFullTableName(targetTable);
                if (tableName?.StartsWith("#") == true)
                {
                    TempTables.Add(new TempTableInfo
                    {
                        TableName = tableName,
                        CreatedInProcedure = CurrentProcedure ?? string.Empty,
                        SourceQuery = node.InsertSpecification.InsertSource.GetScript(),
                        ParentsTable = AnalyzeSourceQuery(node.InsertSpecification.InsertSource.GetScript())
                        //ParentsTable = new List<TableReference>() {
                        //    //new TableReference
                        //    //{
                        //    //    TableName = tableName,
                        //    //    IsTempTable = true,
                        //    //    ReferenceType = "INSERT",
                        //    //    ProcedureName = CurrentProcedure ?? string.Empty
                        //    //}


                        //}
                    });
                }
            }
            base.Visit(node);
        }

        public override void Visit(CommonTableExpression node)
        {
            TempTables.Add(new TempTableInfo
            {
                TableName = node.ExpressionName.Value,
                CreatedInProcedure = CurrentProcedure ?? string.Empty,
                SourceQuery = node.QueryExpression.GetScript(),
                ParentsTable = AnalyzeSourceQuery(node.QueryExpression.GetScript())

            });
            base.Visit(node);
        }

        public override void Visit(CreateTableStatement node)
        {
            var tableName = node.SchemaObjectName.BaseIdentifier.Value;
            if (tableName.StartsWith("#"))
            {
                TempTables.Add(new TempTableInfo
                {
                    TableName = tableName,
                    CreatedInProcedure = CurrentProcedure ?? string.Empty,
                    SourceQuery = node.GetScript(),
                    ParentsTable = AnalyzeSourceQuery(node.GetScript())
                });
            }
            base.Visit(node);
        }

        public override void Visit(CreateProcedureStatement node)
        {
            CurrentProcedure = node.ProcedureReference.Name.BaseIdentifier.Value;
            base.Visit(node);
        }

        private string GetFullTableName(SchemaObjectName schemaObjectName)
        {
            return string.Join(".", schemaObjectName.Identifiers.Select(i => i.Value));
        }

        private string? GetFullTableName(TableReferenceWithAlias tableReference)
        {
            return tableReference is NamedTableReference namedTableReference
                ? string.Join(".", namedTableReference.SchemaObject.Identifiers.Select(i => i.Value))
                : null;
        }

        public override void Visit(JoinTableReference node)
        {
            Visit(node.FirstTableReference);
            Visit(node.SecondTableReference);
            if (node is QualifiedJoin qualifiedJoin)
            {
                Visit(qualifiedJoin.SearchCondition);
            }
            base.Visit(node);
        }
        private List<TableReference> AnalyzeSourceQuery(string sourceQuery)
        {
            var parser = new TSql150Parser(true);
            IList<ParseError> errors;
            var fragment = parser.Parse(new StringReader(sourceQuery), out errors);

            if (errors.Count > 0)
            {
                throw new Exception($"SQL Parse errors: {string.Join("\n", errors)}");
            }

            var tableVisitor = new TableLineageVisitor();
            fragment.Accept(tableVisitor);

            return tableVisitor.Tables;
        }
    }

    /// <summary>
    /// Represents a reference to a table in a T-SQL script.
    /// </summary>
    public class TableReference
    {
        public string ProcedureName { get; set; }
        public string TableName { get; set; }
        public bool IsTempTable { get; set; }
        public string ReferenceType { get; set; }
    }

    /// <summary>
    /// Represents information about a temporary table in a T-SQL script.
    /// </summary>
    public class TempTableInfo
    {
        public string TableName { get; set; }
        public string CreatedInProcedure { get; set; }
        public string SourceQuery { get; set; }
        public List<TableReference> ParentsTable { get; set; } = new();
    }

    public class SqlAnalysisEngine
    {
        public (TableLineageVisitor, ColumnLineageVisitor) AnalyzeSqlFile(string filePath)
        {
            var parser = new TSql150Parser(true);
            IList<ParseError> errors;

            using var textReader = new StreamReader(filePath);
            var fragment = parser.Parse(textReader, out errors);

            if (errors.Count > 0)
            {
                throw new Exception($"SQL Parse errors: {string.Join("\n", errors)}");
            }

            var tableVisitor = new TableLineageVisitor();
            var columnVisitor = new ColumnLineageVisitor();
            fragment.Accept(tableVisitor);
            fragment.Accept(columnVisitor);
            return (tableVisitor, columnVisitor);
        }
    }

    public class ReportGenerator
    {
        public void GenerateReport(TableLineageVisitor tableAnalysisResult, ColumnLineageVisitor columnAnalysisResult)
        {
            Console.WriteLine($"=== Analysis Report for {tableAnalysisResult.CurrentProcedure} ===");

            Console.WriteLine("\nReferenced Tables:");
            foreach (var table in tableAnalysisResult.Tables)
            {
                Console.WriteLine($"- [{(table.IsTempTable ? "TEMP" : "TABLE")}] {table.TableName} ({table.ReferenceType})");
            }

            Console.WriteLine("\nCreated Temp Tables:");
            foreach (var tempTable in tableAnalysisResult.TempTables)
            {
                Console.WriteLine($"- {tempTable.TableName}");
                Console.WriteLine($"  Source: {Truncate(tempTable.SourceQuery, 100)}");
                Console.WriteLine($"  Source Tables: {string.Join(", ", tempTable.ParentsTable.Select(a => a.TableName))}");
            }

            Console.WriteLine("\nReferenced Columns:");
            foreach (var column in columnAnalysisResult.Columns)
            {
                Console.WriteLine($"- {column.TableName}.{column.ColumnName} ({column.ReferenceType}) in {column.ProcedureName}");
            }
        }

        private string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
    /// <summary>
    /// Extension methods for TSqlFragment.
    /// </summary>
    public static class TSqlFragmentExtensions
    {
        public static string GetScript(this TSqlFragment fragment)
        {
            if (fragment == null) return string.Empty;

            var scriptGenerator = new Sql150ScriptGenerator();
            scriptGenerator.GenerateScript(fragment, out string script);
            return script;
        }
    }
    /// <summary>
    /// Represents a reference to a column in a T-SQL script.
    /// </summary>
    public class ColumnReference
    {
        /// <summary>
        /// Gets or sets the name of the procedure where the column is referenced.
        /// </summary>
        public string ProcedureName { get; set; }

        /// <summary>
        /// Gets or sets the name of the table containing the column.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Gets or sets the name of the column.
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Gets or sets the type of reference (e.g., SELECT, INSERT).
        /// </summary>
        public string ReferenceType { get; set; }
    }
    public class ColumnLineageVisitor : TSqlFragmentVisitor
    {
        public string? CurrentProcedure { get; set; } // Make CurrentProcedure nullable

        public readonly List<ColumnReference> Columns = new();

        public override void Visit(SelectStatement node)
        {
            Visit(node.QueryExpression);
            base.Visit(node);
        }

        public override void Visit(QuerySpecification node)
        {
            foreach (var selectElement in node.SelectElements)
            {
                if (selectElement is SelectScalarExpression scalarExpression && scalarExpression.Expression is ColumnReferenceExpression columnReference)
                {
                    var columnName = columnReference.MultiPartIdentifier.Identifiers.Last().Value;
                    var tableName = string.Join(".", columnReference.MultiPartIdentifier.Identifiers.SkipLast(1).Select(i => i.Value));

                    Columns.Add(new ColumnReference
                    {
                        ProcedureName = CurrentProcedure ?? string.Empty,
                        TableName = tableName,
                        ColumnName = columnName,
                        ReferenceType = "SELECT"
                    });
                }
            }
            base.Visit(node);
        }

        public override void Visit(InsertStatement node)
        {
            if (node.InsertSpecification.Target is NamedTableReference targetTable)
            {
                var tableName = GetFullTableName(targetTable);
                foreach (var column in node.InsertSpecification.Columns)
                {
                    Columns.Add(new ColumnReference
                    {
                        ProcedureName = CurrentProcedure ?? string.Empty,
                        TableName = tableName,
                        ColumnName = column.MultiPartIdentifier.Identifiers.Last().Value,
                        ReferenceType = "INSERT"
                    });
                }
            }
            base.Visit(node);
        }

        public override void Visit(CreateProcedureStatement node)
        {
            CurrentProcedure = node.ProcedureReference.Name.BaseIdentifier.Value;
            base.Visit(node);
        }

        private string GetFullTableName(NamedTableReference tableReference)
        {
            return string.Join(".", tableReference.SchemaObject.Identifiers.Select(i => i.Value));
        }
    }


}
