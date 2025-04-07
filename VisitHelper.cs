using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Austin.SqlDataLineageAnalyzer;

namespace Austin
{
    public class VisitHelper : TSqlFragmentVisitor
    {
        private Dictionary<string, TableInfo> _tableRegistry = new Dictionary<string, TableInfo>();
        private Stack<QueryContext> _queryStack = new Stack<QueryContext>();

        public override void Visit(CreateTableStatement node)
        {
            var schemaObject = node.SchemaObjectName;

            if (IsTempTable(schemaObject.BaseIdentifier.Value))
            {
                var tempTable = new TableInfo
                {
                    Name = node.SchemaObjectName.BaseIdentifier.Value,
                    Type = (SqlDataLineageAnalyzer.TableType)TableType.Temp,
                    Columns = GetColumns(node.Definition.ColumnDefinitions)
                };

                _tableRegistry[tempTable.Name] = tempTable;
                _queryStack.Peek().CurrentTable = tempTable;
            }
        }

        private bool IsTempTable(string name) =>
            name.StartsWith("#") || name.Contains("tempdb..#");

        public override void Visit(InsertStatement node)
        {
            if (node.InsertSpecification.Target is NamedTableReference namedTableReference)
            {
                var queryContext = new QueryContext();
                if (node.InsertSpecification.InsertSource is SelectInsertSource sourceQuery)
                {
                    AnalyzeQueryExpression(sourceQuery.Select, queryContext);
                    //AnalyzeSelectStatement(sourceQuery.Select as SelectStatement, queryContext);
                    var targetTable = ResolveTable(namedTableReference.SchemaObject);
                    if (targetTable == null)
                    {
                        // Handle the case where the target table is not found
                        return;
                    }

                    // 记录插入操作的血缘
                    foreach (var col in queryContext.OutputColumns)
                    {
                        _tableRegistry[targetTable].Dependencies.Add(new LineageEdge
                        {
                            FromTable = col.ParentTable.Name,
                            ToTable = targetTable,
                            ColumnName = col.Alias,
                            Operation = "INSERT_INTO"
                        });
                    }
                }
            }
        }

        public override void Visit(CaseExpression node)
        {
            if (node is SearchedCaseExpression searchedCase)
            {
                foreach (SearchedWhenClause when in searchedCase.WhenClauses)
                {
                    AnalyzeExpression(when.WhenExpression);
                }

                AnalyzeExpression(searchedCase.ElseExpression);
            }
            else if (node is SimpleCaseExpression simpleCase)
            {
                foreach (SimpleWhenClause when in simpleCase.WhenClauses)
                {
                    AnalyzeExpression(when.WhenExpression);
                }

                AnalyzeExpression(simpleCase.ElseExpression);
            }
        }

        private void AnalyzeExpression(TSqlFragment expr)
        {
            if (expr is CaseExpression caseExpr) Visit(caseExpr);
            else if (expr is FunctionCall func) AnalyzeFunction(func);
            else if (expr is ScalarSubquery sub) Visit(sub.QueryExpression);
            else if (expr is ColumnReferenceExpression col) TrackColumnDependency(col);
        }

        private void TrackColumnDependency(ColumnReferenceExpression col)
        {
            var column = new ColumnInfo
            {
                OriginalName = col.MultiPartIdentifier.Identifiers.Last().Value,
                ParentTable = _queryStack.Peek().CurrentTable
            };

            column.ParentTable.Columns.Add(column);
            _queryStack.Peek().CurrentTable.Columns.Add(column);
        }

        public override void Visit(ColumnReferenceExpression node)
        {
            // 处理列引用
            var column = new ColumnInfo
            {
                OriginalName = node.MultiPartIdentifier.Identifiers.Last().Value,
                ParentTable = _queryStack.Peek().CurrentTable
            };

            column.ParentTable.Columns.Add(column);
            AnalyzeColumnDependencies(node);
        }

        private void AnalyzeColumnDependencies(ScalarExpression expr)
        {
            // 处理CASE表达式
            if (expr is CaseExpression caseExpr)
            {
                Visit(caseExpr); // 递归处理CASE表达式
            }
            // 处理聚合函数
            else if (expr is FunctionCall function)
            {
                if (IsAggregateFunction(function))
                {
                    foreach (var param in function.Parameters)
                    {
                        AnalyzeExpression(param);
                    }
                }
            }
            // 处理嵌套查询
            else if (expr is ScalarSubquery subquery)
            {
                Visit(subquery.QueryExpression);
            }
        }

        private bool IsAggregateFunction(FunctionCall function)
        {
            // 判断是否为聚合函数的逻辑
            return false;
        }

        private void AnalyzeFunction(FunctionCall func)
        {
            // 分析函数调用的逻辑
        }

        private List<ColumnInfo> GetColumns(IList<ColumnDefinition> columnDefinitions)
        {
            // 获取列定义的逻辑
            return new List<ColumnInfo>();
        }

        private void AnalyzeSelectStatement(SelectStatement selectStatement, QueryContext queryContext)
        {
            // 分析SELECT语句的逻辑
        }
        private void AnalyzeQueryExpression(QueryExpression queryExpression, QueryContext queryContext)
        {
            if (queryExpression is QuerySpecification querySpecification)
            {
                AnalyzeSelectStatement(querySpecification, queryContext);
            }
        }
        // 处理其他类型的QueryExpression
        private string ResolveTable(SchemaObjectName schemaObject)
        {
            return schemaObject.Identifiers.LastOrDefault()?.Value ?? string.Empty;
        }

        private TableInfo ResolveTable(TableReference tableReference)
        {
            if (tableReference is NamedTableReference namedTableReference)
            {
                // 解析表的逻辑
                return new TableInfo
                {
                    Name = namedTableReference.SchemaObject.Identifiers.LastOrDefault()?.Value ?? string.Empty,
                    // 其他属性的初始化
                };
            }
            return null;

        }

        private void AnalyzeSelectStatement(QuerySpecification querySpecification, QueryContext queryContext)
        {
            throw new NotImplementedException();
        }
    }
}
