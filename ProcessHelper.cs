//using Microsoft.SqlServer.TransactSql.ScriptDom;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using static Austin.SqlDataLineageAnalyzer;

//namespace Austin
//{
//    public class ProcessHelper
//    {
//        private readonly Dictionary<string, TempTableInfo> _tableRegistry = new();

//        //private void ProcessFromClause(FromClause fromClause)
//        //{
//        //    if (fromClause is SchemaObjectJoinTableReference joinRef)
//        //    {
//        //        var leftTable = ResolveTable(joinRef.LeftTable);
//        //        var rightTable = ResolveTable(joinRef.RightTable);

//        //        _tableRegistry[leftTable.Name].Dependencies.Add(new LineageEdge
//        //        {
//        //            FromTable = leftTable.Name,
//        //            ToTable = rightTable.Name,
//        //            Operation = joinRef.JoinType.ToString()
//        //        });

//        //        _tableRegistry[rightTable.Name].Dependencies.Add(new LineageEdge
//        //        {
//        //            FromTable = rightTable.Name,
//        //            ToTable = leftTable.Name,
//        //            Operation = joinRef.JoinType.ToString()
//        //        });
//        //    }
//        //}
//        private void ProcessSelectElements(List<SelectElement> elements)
//        {
//            foreach (var element in elements)
//            {
//                if (element is SelectScalarExpression scalar)
//                {
//                    var column = new ColumnInfo
//                    {
//                        OriginalName = scalar.Expression.ToString(),
//                        ParentTable = _queryStack.Peek().CurrentTable
//                    };

//                    AnalyzeExpression(scalar.Expression);
//                    _queryStack.Peek().CurrentTable.Columns.Add(column);
//                }
//            }
//        }
//        private void ProcessJoinClause(JoinClause joinClause)
//        {
//            var leftTable = ResolveTable(joinClause.LeftTable);
//            var rightTable = ResolveTable(joinClause.RightTable);

//            // 记录JOIN关系
//            _tableRegistry[leftTable.Name].Dependencies.Add(new LineageEdge
//            {
//                FromTable = leftTable.Name,
//                ToTable = rightTable.Name,
//                Operation = joinClause.JoinType.ToString()
//            });

//            _tableRegistry[rightTable.Name].Dependencies.Add(new LineageEdge
//            {
//                FromTable = rightTable.Name,
//                ToTable = leftTable.Name,
//                Operation = joinClause.JoinType.ToString()
//            });
//        }
//        private void ProcessSubquery(SubqueryExpression subquery)
//        {
//            var tempTable = new TableInfo
//            {
//                Name = GenerateSubqueryAlias(subquery),
//                Type = TableType.Temp,
//                Dependencies = AnalyzeQueryExpression(subquery.Query)
//            };

//            _tableRegistry[tempTable.Name] = tempTable;
//            subquery.SchemaObject = new SchemaObjectName { Identifier = new Identifier { Value = tempTable.Name } };
//        }


//        private void ProcessFromClause(FromClause fromClause)
//        {
//            foreach (var tableRef in fromClause.TableReferences)
//            {
//                if (tableRef is JoinTableReference joinRef)
//                {
//                    // 获取左右表引用
//                    var leftRef = joinRef.FirstTableReference as TableReference;
//                    var rightRef = joinRef.SecondTableReference as TableReference;

//                    // 解析实际表名（兼容多版本）
//                    var leftTable = ResolveTable(leftRef);
//                    var rightTable = ResolveTable(rightRef);

//                    // 获取连接类型
//                    var joinType = joinRef.JoinType switch
//                    {
//                        JoinType.Inner => "INNER JOIN",
//                        JoinType.LeftOuter => "LEFT OUTER JOIN",
//                        JoinType.RightOuter => "RIGHT OUTER JOIN",
//                        _ => "UNKNOWN JOIN"
//                    };

//                    // 记录依赖关系
//                    if (leftTable != null && rightTable != null)
//                    {
//                        _tableRegistry[leftTable.Name].Dependencies.Add(new LineageEdge
//                        {
//                            FromTable = leftTable.Name,
//                            ToTable = rightTable.Name,
//                            Operation = joinType
//                        });

//                        _tableRegistry[rightTable.Name].Dependencies.Add(new LineageEdge
//                        {
//                            FromTable = rightTable.Name,
//                            ToTable = leftTable.Name,
//                            Operation = joinType
//                        });
//                    }
//                }
//            }
//        }

//        private TableInfo ResolveTable(TableReference tableRef)
//        {
//            if (tableRef is SchemaObjectTableReference schemaTable)
//            {
//                return new TableInfo
//                {
//                    Name = $"{schemaTable.SchemaObjectName.Schema}.{schemaTable.SchemaObjectName.Name}"
//                };
//            }

//            if (tableRef is TempTableReference tempTable)
//            {
//                return new TableInfo
//                {
//                    Name = tempTable.Name.Identifier.Value
//                };
//            }

//            return new TableInfo { Name = tableRef.ToString() };
//        }
//    }
//}
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Austin.SqlDataLineageAnalyzer;

namespace Austin
{
    public class ProcessHelper
    {
        private readonly Dictionary<string, TableInfo> _tableRegistry = new();
        private readonly Stack<QueryContext> _queryStack = new();

        private void ProcessSelectElements(List<SelectElement> elements)
        {
            foreach (var element in elements)
            {
                if (element is SelectScalarExpression scalar)
                {
                    var column = new ColumnInfo
                    {
                        OriginalName = scalar.Expression.ToString(),
                        ParentTable = _queryStack.Peek().CurrentTable
                    };

                    AnalyzeExpression(scalar.Expression);
                    _queryStack.Peek().CurrentTable.Columns.Add(column);
                }
            }
        }

        private void ProcessJoinClause(JoinTableReference joinRef)
        {
            var leftTable = ResolveTable(joinRef.FirstTableReference);
            var rightTable = ResolveTable(joinRef.SecondTableReference);

            // 记录JOIN关系
            _tableRegistry[leftTable.Name].Dependencies.Add(new LineageEdge
            {
                FromTable = leftTable.Name,
                ToTable = rightTable.Name,
                Operation = joinRef.JoinHint.ToString()
            });

            _tableRegistry[rightTable.Name].Dependencies.Add(new LineageEdge
            {
                FromTable = rightTable.Name,
                ToTable = leftTable.Name,
                Operation = joinRef.JoinHint.ToString()
            });
        }

        private void ProcessSubquery(ScalarSubquery subquery)
        {
            var tempTable = new TableInfo
            {
                Name = GenerateSubqueryAlias(subquery),
                Type = TableType.Temp,
                Dependencies = AnalyzeQueryExpression(subquery.QueryExpression)
            };

            _tableRegistry[tempTable.Name] = tempTable;
            subquery.QueryExpression = new QuerySpecification { SelectElements = { new SelectScalarExpression { ColumnName = new Identifier { Value = tempTable.Name } } } };
        }

        private void ProcessFromClause(FromClause fromClause)
        {
            foreach (var tableRef in fromClause.TableReferences)
            {
                if (tableRef is JoinTableReference joinRef)
                {
                    // 获取左右表引用
                    var leftRef = joinRef.FirstTableReference as TableReference;
                    var rightRef = joinRef.SecondTableReference as TableReference;

                    // 解析实际表名（兼容多版本）
                    var leftTable = ResolveTable(leftRef);
                    var rightTable = ResolveTable(rightRef);

                    //// 获取连接类型
                    //var joinType = joinRef.JoinHint switch
                    //{
                    //    JoinHint.None => "INNER JOIN",
                    //    JoinHint.Left => "LEFT OUTER JOIN",
                    //    JoinHint.Right => "RIGHT OUTER JOIN",
                    //    _ => "UNKNOWN JOIN"
                    //};

                    // 记录依赖关系
                    if (leftTable != null && rightTable != null)
                    {
                        _tableRegistry[leftTable.Name].Dependencies.Add(new LineageEdge
                        {
                            FromTable = leftTable.Name,
                            ToTable = rightTable.Name,
                            //Operation = joinType
                        });

                        _tableRegistry[rightTable.Name].Dependencies.Add(new LineageEdge
                        {
                            FromTable = rightTable.Name,
                            ToTable = leftTable.Name,
                            //Operation = joinType
                        });
                    }
                }
            }
        }

        private TableInfo ResolveTable(TableReference tableRef)
        {
            if (tableRef is NamedTableReference schemaTable)
            {
                return new TableInfo
                {
                    Name = $"{schemaTable.SchemaObject.SchemaIdentifier.Value}.{schemaTable.SchemaObject.BaseIdentifier.Value}"
                };
            }

            if (tableRef is NamedTableReference tempTable)
            {
                return new TableInfo
                {
                    Name = tempTable.SchemaObject.BaseIdentifier.Value
                };
            }

            return new TableInfo { Name = tableRef.ToString() };
        }

        private string GenerateSubqueryAlias(ScalarSubquery subquery)
        {
            // Implement the logic to generate a unique alias for the subquery
            return "SubqueryAlias";
        }

        private List<LineageEdge> AnalyzeQueryExpression(QueryExpression queryExpression)
        {
            // Implement the logic to analyze the query expression and return the dependencies
            return new List<LineageEdge>();
        }

        private void AnalyzeExpression(ScalarExpression expression)
        {
            // Implement the logic to analyze the scalar expression
        }
    }

    public class QueryContext
    {
        public TableInfo CurrentTable { get; set; }
    }
}
